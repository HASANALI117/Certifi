using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Hubs;

namespace TrainingPlatform.MVC.Controllers;

// Stripe Checkout payment flow. Trainees pay their outstanding enrollment fee via a
// Stripe-hosted page; the webhook is the single source of truth that writes the
// Payment row. There is no manual/offline payment path — all money flows through Stripe.
[Authorize]
public class PaymentsController(
    AppDbContext context,
    IConfiguration config,
    IHubContext<EnrollmentHub> hub,
    ILogger<PaymentsController> logger) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly IConfiguration _config = config;
    private readonly IHubContext<EnrollmentHub> _hub = hub;
    private readonly ILogger<PaymentsController> _logger = logger;

    private string Currency => (_config["Stripe:Currency"] ?? "bhd").ToLowerInvariant();

    // Create a Stripe Checkout Session for an enrollment's (partial) balance and return
    // the hosted-page URL for the browser to redirect to.
    [Authorize(Roles = "Trainee")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession(int enrollmentId, decimal amount)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Trainee)
            .Include(e => e.CourseSession).ThenInclude(cs => cs.Course)
            .Include(e => e.Payments)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId);

        if (enrollment == null) return NotFound();

        if (!await CurrentUserOwnsAsync(enrollment))
            return Json(new { ok = false, message = "You can only pay for your own enrollment." });

        if (enrollment.Status == EnrollmentStatus.Dropped)
            return Json(new { ok = false, message = "Dropped enrollments cannot accept payments." });

        var balance = OutstandingBalance(enrollment);
        if (amount <= 0 || amount > balance)
            return Json(new { ok = false, message = "Amount must be greater than zero and no more than the outstanding balance." });

        if (string.IsNullOrEmpty(StripeConfiguration.ApiKey))
        {
            _logger.LogError("Stripe:SecretKey is not configured.");
            return Json(new { ok = false, message = "Payments are not configured. Contact the coordinator." });
        }

        var courseTitle = enrollment.CourseSession?.Course?.Title ?? "Course enrollment";
        var origin = $"{Request.Scheme}://{Request.Host}";

        // Convert to Stripe's smallest currency unit (cents/fils). Factor depends on
        // the currency's decimal places (2 for usd/aed/sar, 3 for bhd/kwd, 0 for jpy).
        var unitAmount = (long)Math.Round(amount * MinorUnitFactor(Currency), MidpointRounding.AwayFromZero);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = enrollment.Id.ToString(),
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = Currency,
                        UnitAmount = unitAmount,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = courseTitle,
                            Description = "Training enrollment fee payment"
                        }
                    }
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["enrollmentId"] = enrollment.Id.ToString(),
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture)
            },
            SuccessUrl = $"{origin}/Enrollments/BillingAndAlerts?payment=success",
            CancelUrl = $"{origin}/Enrollments/BillingAndAlerts?payment=cancelled"
        };

        try
        {
            var session = await new SessionService().CreateAsync(options);
            return Json(new { ok = true, redirect = session.Url });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout session creation failed for enrollment {Id}.", enrollmentId);
            return Json(new { ok = false, message = "Could not start the payment. Please try again." });
        }
    }

    // Stripe webhook — authoritative payment confirmation. Anonymous + no antiforgery;
    // authenticity is verified with the webhook signing secret instead.
    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        var webhookSecret = _config["Stripe:WebhookSecret"];
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(webhookSecret))
            return BadRequest();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest();
        }

        if (stripeEvent.Type == "checkout.session.completed")
        {
            var session = stripeEvent.Data.Object as Session;
            if (session is not null && session.PaymentStatus == "paid")
                await ApplyPaymentAsync(session);
        }

        return Ok();
    }

    // Writes the Payment row for a paid Checkout Session. Idempotent across Stripe's
    // delivery retries via the StripeSessionId guard.
    private async Task ApplyPaymentAsync(Session session)
    {
        if (await _context.Payments.AnyAsync(p => p.StripeSessionId == session.Id))
            return; // already processed

        if (!session.Metadata.TryGetValue("enrollmentId", out var enrollmentIdRaw)
            || !int.TryParse(enrollmentIdRaw, out var enrollmentId))
        {
            _logger.LogWarning("Stripe session {Id} missing enrollmentId metadata.", session.Id);
            return;
        }

        var enrollment = await _context.Enrollments
            .Include(e => e.Trainee)
            .Include(e => e.CourseSession).ThenInclude(cs => cs.Course)
            .Include(e => e.Payments)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId);
        if (enrollment == null) return;

        // Trust the actual amount Stripe collected, converted back from minor units.
        var paidAmount = (session.AmountTotal ?? 0L) / MinorUnitFactor(Currency);
        var balance = OutstandingBalance(enrollment);
        var applied = Math.Min(paidAmount, balance);
        var newBalance = Math.Max(0m, balance - applied);

        _context.Payments.Add(new Payment
        {
            EnrollmentId = enrollment.Id,
            AmountPaid = applied,
            PaidAt = DateTime.UtcNow,
            OutstandingBalance = newBalance,
            StripeSessionId = session.Id
        });

        await AddTraineeNotificationAsync(enrollment.Trainee.UserId,
            $"Payment of {applied:0.000} {Currency.ToUpperInvariant()} received for {enrollment.CourseSession?.Course?.Title}. Remaining balance: {newBalance:0.000}.",
            "Payment");

        if (newBalance <= 0 && enrollment.Status == EnrollmentStatus.Enrolled)
        {
            enrollment.Status = EnrollmentStatus.Confirmed;
            await AddTraineeNotificationAsync(enrollment.Trainee.UserId,
                $"Your enrollment for {enrollment.CourseSession?.Course?.Title} is confirmed after full payment.",
                "Enrollment");
        }

        await _context.SaveChangesAsync();

        await _hub.Clients.User(enrollment.Trainee.UserId).SendAsync("NotificationReceived", new
        {
            Message = "Payment received.",
            Type = "Payment",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        await _hub.Clients.All.SendAsync("DashboardRefreshRequested", new { Source = "Payments", At = DateTime.UtcNow });
    }

    private async Task AddTraineeNotificationAsync(string userId, string message, string type)
    {
        if (string.IsNullOrEmpty(userId)) return;
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<bool> CurrentUserOwnsAsync(Enrollment enrollment)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return false;
        var traineeUserId = await _context.Trainees
            .Where(t => t.Id == enrollment.TraineeId)
            .Select(t => t.UserId)
            .FirstOrDefaultAsync();
        return traineeUserId == userId;
    }

    // Stripe expresses amounts in a currency's smallest unit. Most currencies use 2
    // decimals (x100); three-decimal currencies (bhd, kwd, omr, etc.) use x1000;
    // zero-decimal currencies (jpy, krw, etc.) use x1.
    private static decimal MinorUnitFactor(string currency) => currency.ToLowerInvariant() switch
    {
        "bhd" or "kwd" or "omr" or "jod" or "tnd" => 1000m,
        "jpy" or "krw" or "vnd" or "clp" or "xof" or "xaf" or "pyg" or "ugx" or "rwf" or "gnf" or "bif" or "djf" or "kmf" or "mga" or "vuv" or "xpf" => 1m,
        _ => 100m
    };

    private static decimal OutstandingBalance(Enrollment enrollment)
    {
        var fee = enrollment.CourseSession?.Course?.EnrollmentFee ?? 0m;
        var paid = enrollment.Payments?.Sum(p => p.AmountPaid) ?? 0m;
        return Math.Max(0m, fee - paid);
    }
}
