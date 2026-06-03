using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Hubs;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize]
public class EnrollmentsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IHubContext<EnrollmentHub> _hubContext;
    private readonly List<(string UserId, Notification Notification)> _pendingNotifications = [];

    public EnrollmentsController(AppDbContext context, IHubContext<EnrollmentHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // True when the request came from the reusable popup component (tp-modal.js).
    private bool IsModal => Request.Headers.ContainsKey("X-Tp-Modal");
    private IActionResult JsonOk(string message, string type = "Success") => Json(new { ok = true, message, type });
    private IActionResult JsonFail(string message) => Json(new { ok = false, message });

    private async Task<IActionResult> EnrollmentFormResult(Enrollment enrollment)
    {
        await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
        return PartialView("_EnrollmentForm", enrollment);
    }

    // These actions don't have their own pages. Everything happens on the Manage page in a popup.
    [Authorize(Roles = "TrainingCoordinator")]
    public IActionResult Index() => RedirectToAction(nameof(Manage));

    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Manage()
    {
        var enrollments = await EnrollmentQuery()
            .OrderBy(e => e.CourseSession.StartDateTime)
            .ThenBy(e => e.Trainee.User.LastName)
            .ToListAsync();

        await FlagOverduePaymentsAsync(enrollments);

        return View(new ManageEnrollmentsViewModel
        {
            Enrollments = enrollments,
            PaymentStatuses = enrollments.ToDictionary(e => e.Id, PaymentStatus)
        });
    }

    // Instructors get their own assessment roster, scoped to sessions they teach. No payment data.
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> Roster()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
        if (instructor == null)
        {
            TempData["Error"] = "Instructor profile not found.";
            return View(new InstructorRosterViewModel());
        }

        var enrollments = await EnrollmentQuery()
            .Where(e => e.CourseSession.InstructorId == instructor.Id)
            .Where(e => e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.Attending || e.Status == EnrollmentStatus.Completed)
            .OrderBy(e => e.CourseSession.StartDateTime)
            .ThenBy(e => e.Trainee.User.LastName)
            .ToListAsync();

        return View(new InstructorRosterViewModel { Enrollments = enrollments });
    }

    [Authorize(Roles = "Trainee")]
    public async Task<IActionResult> BillingAndAlerts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var trainee = await _context.Trainees
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (trainee == null)
        {
            TempData["Error"] = "Trainee profile not found.";
            return View(new TraineeBillingViewModel());
        }

        var enrollments = await EnrollmentQuery()
            .Where(e => e.TraineeId == trainee.Id)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        await FlagOverduePaymentsAsync(enrollments);

        return View(new TraineeBillingViewModel
        {
            Enrollments = enrollments,
            PaymentStatuses = enrollments.ToDictionary(e => e.Id, PaymentStatus),
            OutstandingBalances = enrollments.ToDictionary(e => e.Id, OutstandingBalance),
            Notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync(),
            Certifications = await _context.TraineeCertifications
                .Include(c => c.CertificationTrack)
                .Where(c => c.TraineeId == trainee.Id)
                .OrderBy(c => c.CertificationTrack.Name)
                .ToListAsync()
        });
    }

    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Details(int id)
    {
        if (!IsModal) return RedirectToAction(nameof(Manage));
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        return enrollment == null ? NotFound() : PartialView("_EnrollmentDetails", enrollment);
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!IsModal) return RedirectToAction(nameof(Manage));
        await SetEnrollmentSelectListsAsync();
        return PartialView("_EnrollmentForm", new Enrollment { EnrolledAt = DateTime.Now });
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TraineeId,CourseSessionId,Status,EnrolledAt")] Enrollment enrollment)
    {
        ClearEnrollmentNavigationValidation();

        if (!ModelState.IsValid)
        {
            return await EnrollmentFormResult(enrollment);
        }

        var session = await _context.CourseSessions
            .Include(cs => cs.Course)
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == enrollment.CourseSessionId);

        if (session == null)
        {
            ModelState.AddModelError(nameof(enrollment.CourseSessionId), "Selected course session was not found.");
            return await EnrollmentFormResult(enrollment);
        }

        if (ActiveEnrollmentCount(session) >= session.Capacity)
        {
            ModelState.AddModelError(nameof(enrollment.CourseSessionId), "This course session is full.");
            return await EnrollmentFormResult(enrollment);
        }

        // (TraineeId, CourseSessionId) is unique and a dropped session is not re-joinable.
        var existing = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.TraineeId == enrollment.TraineeId &&
                                      e.CourseSessionId == enrollment.CourseSessionId);

        if (existing != null)
        {
            ModelState.AddModelError(nameof(enrollment.TraineeId), existing.Status == EnrollmentStatus.Dropped
                ? "This trainee previously dropped this session — choose a different session."
                : "This trainee is already actively enrolled in the selected session.");
            return await EnrollmentFormResult(enrollment);
        }

        enrollment.EnrolledAt = enrollment.EnrolledAt == default ? DateTime.UtcNow : enrollment.EnrolledAt;
        _context.Enrollments.Add(enrollment);

        await StartCertificationTrackingAsync(enrollment.TraineeId, session.CourseId);
        await AddNotificationForTraineeAsync(enrollment.TraineeId, $"You were enrolled in {session.Course.Title}.", "Enrollment");
        await SaveChangesAndNotifyAsync();
        await BroadcastEnrollmentCountAsync(enrollment.CourseSessionId);

        if (IsModal) return JsonOk("Enrollment created.");
        TempData["Success"] = "Enrollment created.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsModal) return RedirectToAction(nameof(Manage));
        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null) return NotFound();

        await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
        return PartialView("_EnrollmentForm", enrollment);
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TraineeId,CourseSessionId,Status,EnrolledAt")] Enrollment model)
    {
        if (id != model.Id) return NotFound();

        ClearEnrollmentNavigationValidation();

        if (!ModelState.IsValid)
        {
            return await EnrollmentFormResult(model);
        }

        var session = await _context.CourseSessions
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == model.CourseSessionId);

        if (session == null)
        {
            ModelState.AddModelError(nameof(model.CourseSessionId), "Selected course session was not found.");
            return await EnrollmentFormResult(model);
        }

        var duplicate = await _context.Enrollments.AnyAsync(e =>
            e.Id != id &&
            e.TraineeId == model.TraineeId &&
            e.CourseSessionId == model.CourseSessionId &&
            e.Status != EnrollmentStatus.Dropped);

        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.TraineeId), "This trainee is already actively enrolled in the selected session.");
            return await EnrollmentFormResult(model);
        }

        var activeEnrollmentCount = session.Enrollments.Count(e => e.Id != id && e.Status != EnrollmentStatus.Dropped);
        if (model.Status != EnrollmentStatus.Dropped && activeEnrollmentCount >= session.Capacity)
        {
            ModelState.AddModelError(nameof(model.CourseSessionId), "This course session is full.");
            return await EnrollmentFormResult(model);
        }

        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null) return NotFound();

        var oldSessionId = enrollment.CourseSessionId;
        var oldStatus = enrollment.Status;

        enrollment.TraineeId = model.TraineeId;
        enrollment.CourseSessionId = model.CourseSessionId;
        enrollment.Status = model.Status;
        enrollment.EnrolledAt = model.EnrolledAt;

        if (oldStatus != model.Status)
            await AddNotificationForTraineeAsync(model.TraineeId, $"Your enrollment status changed to {model.Status}.", "Enrollment");

        await StartCertificationTrackingAsync(model.TraineeId, session.CourseId);
        await SaveChangesAndNotifyAsync();
        await BroadcastEnrollmentCountAsync(oldSessionId);

        if (oldSessionId != model.CourseSessionId)
            await BroadcastEnrollmentCountAsync(model.CourseSessionId);

        if (IsModal) return JsonOk("Enrollment updated.");
        TempData["Success"] = "Enrollment updated.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsModal) return RedirectToAction(nameof(Manage));
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        return enrollment == null ? NotFound() : PartialView("_EnrollmentDelete", enrollment);
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Payments)
            .Include(e => e.Assessment)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (enrollment == null) return NotFound();

        var sessionId = enrollment.CourseSessionId;
        _context.Payments.RemoveRange(enrollment.Payments);
        if (enrollment.Assessment != null)
            _context.Assessments.Remove(enrollment.Assessment);

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync();
        await BroadcastEnrollmentCountAsync(sessionId);

        if (IsModal) return JsonOk("Enrollment deleted.");
        TempData["Success"] = "Enrollment deleted.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Trainee")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int courseSessionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
        if (trainee == null)
        {
            TempData["Error"] = "Trainee profile not found.";
            return RedirectToAction("Index", "CourseSessions");
        }

        // Use a serializable transaction so two people can't grab the last seat at the same time.
        await using var tx = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var session = await _context.CourseSessions
            .Include(cs => cs.Course)
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

        if (session == null)
        {
            TempData["Error"] = "Session not found.";
            return RedirectToAction("Index", "CourseSessions");
        }

        // You can only enroll in a scheduled session that hasn't started yet.
        if (session.Status != SessionStatus.Scheduled || session.StartDateTime <= DateTime.UtcNow)
        {
            TempData["Error"] = "This session is no longer open for enrollment.";
            return RedirectToAction("Index", "CourseSessions");
        }

        if (ActiveEnrollmentCount(session) >= session.Capacity)
        {
            TempData["Error"] = "This session is full.";
            return RedirectToAction("Index", "CourseSessions");
        }

        // A trainee can only be in a session once, and can't rejoin one they dropped.
        var existing = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.TraineeId == trainee.Id && e.CourseSessionId == courseSessionId);

        if (existing != null)
        {
            TempData["Error"] = existing.Status == EnrollmentStatus.Dropped
                ? "You previously dropped this session. Enroll in another upcoming session of this course."
                : "You are already enrolled.";
            return RedirectToAction("Index", "CourseSessions");
        }

        _context.Enrollments.Add(new Enrollment
        {
            TraineeId = trainee.Id,
            CourseSessionId = courseSessionId,
            Status = EnrollmentStatus.Enrolled,
            EnrolledAt = DateTime.UtcNow
        });

        await StartCertificationTrackingAsync(trainee.Id, session.CourseId);
        AddNotification(userId, $"Enrolled in {session.Course.Title}.", "Enrollment");
        await SaveChangesAndNotifyAsync();
        await tx.CommitAsync();

        await BroadcastEnrollmentCountAsync(courseSessionId);

        TempData["Success"] = "Enrollment successful.";
        return RedirectToAction("Index", "CourseSessions");
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, EnrollmentStatus newStatus)
    {
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        if (enrollment == null) return NotFound();

        var oldStatus = enrollment.Status;
        enrollment.Status = newStatus;

        if (oldStatus != newStatus)
        {
            await AddNotificationForTraineeAsync(
                enrollment.TraineeId,
                $"Your enrollment for {enrollment.CourseSession.Course.Title} is now {newStatus}.",
                "Enrollment");
        }

        await StartCertificationTrackingForEnrollmentAsync(enrollment.Id);
        await SaveChangesAndNotifyAsync();

        if (oldStatus == EnrollmentStatus.Dropped || newStatus == EnrollmentStatus.Dropped)
            await BroadcastEnrollmentCountAsync(enrollment.CourseSessionId);

        TempData["Success"] = "Enrollment status updated.";
        return RedirectToAction(nameof(Manage));
    }

    // This just shows the payment popup. Stripe handles the actual payment.
    [Authorize(Roles = "Trainee")]
    [HttpGet]
    public async Task<IActionResult> PaymentForm(int id)
    {
        if (!IsModal) return RedirectAfterPayment();
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        if (enrollment == null) return NotFound();
        if (!await CurrentUserOwnsEnrollmentAsync(id)) return Forbid();
        return PartialView("_PaymentForm", enrollment);
    }

    [Authorize(Roles = "TrainingCoordinator,Instructor")]
    [HttpGet]
    public async Task<IActionResult> AssessmentForm(int id)
    {
        if (!IsModal) return RedirectToEnrollmentList();
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        if (enrollment == null) return NotFound();
        return PartialView("_AssessmentForm", enrollment);
    }

    // There's no manual payment option anymore. All payments go through Stripe.
    [Authorize(Roles = "TrainingCoordinator,Instructor")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordAssessment(int enrollmentId, AssessmentResult result, string? notes)
    {
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == enrollmentId);
        if (enrollment == null) return NotFound();

        var recordedById = await GetAssessmentRecorderIdAsync(enrollment);
        if (recordedById == null)
        {
            if (IsModal) return JsonFail("No instructor profile was found for recording this assessment.");
            TempData["Error"] = "No instructor profile was found for recording this assessment.";
            return RedirectToEnrollmentList();
        }

        var assessment = await _context.Assessments.FirstOrDefaultAsync(a => a.EnrollmentId == enrollmentId);
        if (assessment == null)
        {
            assessment = new Assessment { EnrollmentId = enrollmentId };
            _context.Assessments.Add(assessment);
        }

        assessment.Result = result;
        assessment.Notes = notes?.Trim() ?? string.Empty;
        assessment.RecordedAt = DateTime.UtcNow;
        assessment.RecordedById = recordedById.Value;
        enrollment.Status = result == AssessmentResult.Pass ? EnrollmentStatus.Completed : EnrollmentStatus.Attending;

        await AddNotificationForTraineeAsync(
            enrollment.TraineeId,
            $"Assessment recorded for {enrollment.CourseSession.Course.Title}: {result}.",
            "Assessment");

        await SaveChangesAndNotifyAsync();

        // Check certification progress after each assessment. It can move up or down, but won't undo a certificate that was already issued.
        await UpdateCertificationTrackingAsync(enrollment.TraineeId);
        await SaveChangesAndNotifyAsync();

        if (IsModal) return JsonOk("Assessment recorded.", "Assessment");
        TempData["Success"] = "Assessment recorded.";
        return RedirectToEnrollmentList();
    }

    private IQueryable<Enrollment> EnrollmentQuery() =>
        _context.Enrollments
            .Include(e => e.Trainee).ThenInclude(t => t.User)
            .Include(e => e.CourseSession).ThenInclude(cs => cs.Course)
            .Include(e => e.CourseSession).ThenInclude(cs => cs.Classroom)
            .Include(e => e.CourseSession).ThenInclude(cs => cs.Instructor).ThenInclude(i => i.User)
            .Include(e => e.Payments)
            .Include(e => e.Assessment);

    private async Task SetEnrollmentSelectListsAsync(int? traineeId = null, int? courseSessionId = null)
    {
        var trainees = await _context.Trainees
            .Include(t => t.User)
            .OrderBy(t => t.User.LastName)
            .ThenBy(t => t.User.FirstName)
            .ToListAsync();

        ViewBag.TraineeId = new SelectList(
            trainees.Select(t => new { t.Id, Name = $"{FullName(t.User)} ({t.TraineePublicId})" }),
            "Id",
            "Name",
            traineeId);

        var sessions = await _context.CourseSessions
            .Include(cs => cs.Course)
            .OrderBy(cs => cs.StartDateTime)
            .ToListAsync();

        ViewBag.CourseSessionId = new SelectList(
            sessions.Select(s => new { s.Id, Name = $"{s.Course.Title} - {s.StartDateTime:dd MMM yyyy HH:mm}" }),
            "Id",
            "Name",
            courseSessionId);
    }

    private void ClearEnrollmentNavigationValidation()
    {
        ModelState.Remove(nameof(Enrollment.Trainee));
        ModelState.Remove(nameof(Enrollment.CourseSession));
        ModelState.Remove(nameof(Enrollment.Payments));
    }

    private async Task<bool> CurrentUserOwnsEnrollmentAsync(int enrollmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _context.Enrollments
            .Include(e => e.Trainee)
            .AnyAsync(e => e.Id == enrollmentId && e.Trainee.UserId == userId);
    }

    private async Task<int?> GetAssessmentRecorderIdAsync(Enrollment enrollment)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentInstructorId = await _context.Instructors
            .Where(i => i.UserId == userId)
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync();

        return currentInstructorId ?? enrollment.CourseSession.InstructorId;
    }

    private async Task StartCertificationTrackingForEnrollmentAsync(int enrollmentId)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.CourseSession)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId);

        if (enrollment != null)
            await StartCertificationTrackingAsync(enrollment.TraineeId, enrollment.CourseSession.CourseId);
    }

    private async Task StartCertificationTrackingAsync(int traineeId, int courseId)
    {
        var trackIds = await _context.CertificationTrackCourses
            .Where(ctc => ctc.CourseId == courseId)
            .Select(ctc => ctc.CertificationTrackId)
            .Distinct()
            .ToListAsync();

        foreach (var trackId in trackIds)
        {
            var exists = await _context.TraineeCertifications
                .AnyAsync(c => c.TraineeId == traineeId && c.CertificationTrackId == trackId);

            if (!exists)
            {
                _context.TraineeCertifications.Add(new TraineeCertification
                {
                    TraineeId = traineeId,
                    CertificationTrackId = trackId,
                    Status = CertificationStatus.InProgress
                });
            }
        }
    }

    private async Task UpdateCertificationTrackingAsync(int traineeId)
    {
        var passedCourseIds = await _context.Enrollments
            .Where(e => e.TraineeId == traineeId && e.Assessment != null && e.Assessment.Result == AssessmentResult.Pass)
            .Select(e => e.CourseSession.CourseId)
            .Distinct()
            .ToListAsync();

        var tracks = await _context.CertificationTracks
            .Include(t => t.CertificationTrackCourses)
            .ToListAsync();

        foreach (var track in tracks)
        {
            var requiredCourseIds = track.CertificationTrackCourses
                .Where(ctc => ctc.IsRequired)
                .Select(ctc => ctc.CourseId)
                .ToList();

            // No requirements wired up → nothing to track.
            if (requiredCourseIds.Count == 0) continue;

            var certification = await _context.TraineeCertifications
                .FirstOrDefaultAsync(c => c.TraineeId == traineeId && c.CertificationTrackId == track.Id);

            // Issued certificates are real-world artifacts; never silently revoke.
            if (certification?.Status == CertificationStatus.Issued) continue;

            var allRequiredPassed = requiredCourseIds.All(passedCourseIds.Contains);
            var anyRequiredPassed = requiredCourseIds.Any(passedCourseIds.Contains);

            // Promotion: create/upgrade to Eligible when fully qualified.
            if (allRequiredPassed)
            {
                if (certification == null)
                {
                    certification = new TraineeCertification
                    {
                        TraineeId = traineeId,
                        CertificationTrackId = track.Id,
                        Status = CertificationStatus.Eligible
                    };
                    _context.TraineeCertifications.Add(certification);
                    await AddNotificationForTraineeAsync(
                        traineeId,
                        $"You are eligible for certification: {track.Name}.",
                        "Certification");
                }
                else if (certification.Status == CertificationStatus.InProgress)
                {
                    certification.Status = CertificationStatus.Eligible;
                    await AddNotificationForTraineeAsync(
                        traineeId,
                        $"You are eligible for certification: {track.Name}.",
                        "Certification");
                }
                continue;
            }

            // If they no longer qualify, move the certificate back to in progress and let them know.
            if (certification != null && certification.Status == CertificationStatus.Eligible)
            {
                certification.Status = CertificationStatus.InProgress;
                await AddNotificationForTraineeAsync(
                    traineeId,
                    $"Certification eligibility for {track.Name} was revoked after an assessment change.",
                    "Certification");
                continue;
            }

            // Start tracking once at least one required course is passed.
            if (certification == null && anyRequiredPassed)
            {
                _context.TraineeCertifications.Add(new TraineeCertification
                {
                    TraineeId = traineeId,
                    CertificationTrackId = track.Id,
                    Status = CertificationStatus.InProgress
                });
            }
        }
    }

    private async Task AddNotificationForTraineeAsync(int traineeId, string message, string type)
    {
        var userId = await _context.Trainees
            .Where(t => t.Id == traineeId)
            .Select(t => t.UserId)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(userId))
            AddNotification(userId, message, type);
    }

    private void AddNotification(string userId, string message, string type)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        _pendingNotifications.Add((userId, notification));
    }

    private async Task SaveChangesAndNotifyAsync()
    {
        await _context.SaveChangesAsync();

        foreach (var pending in _pendingNotifications)
        {
            await _hubContext.Clients.User(pending.UserId).SendAsync("NotificationReceived", new
            {
                pending.Notification.Id,
                pending.Notification.Message,
                pending.Notification.Type,
                CreatedAt = pending.Notification.CreatedAt,
                pending.Notification.IsRead
            });
        }

        _pendingNotifications.Clear();

        await _hubContext.Clients.All.SendAsync("DashboardRefreshRequested", new
        {
            Source = "Enrollments",
            At = DateTime.UtcNow
        });
    }

    private async Task BroadcastEnrollmentCountAsync(int courseSessionId)
    {
        var session = await _context.CourseSessions
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

        if (session == null) return;

        var enrolledCount = ActiveEnrollmentCount(session);
        var remainingSpots = Math.Max(0, session.Capacity - enrolledCount);

        await _hubContext.Clients.All.SendAsync("EnrollmentUpdated", new
        {
            CourseSessionId = courseSessionId,
            EnrolledCount = enrolledCount,
            Capacity = session.Capacity,
            RemainingSpots = remainingSpots,
            IsFull = remainingSpots <= 0
        });
    }

    private IActionResult RedirectAfterPayment() =>
        User.IsInRole("Trainee")
            ? RedirectToAction(nameof(BillingAndAlerts))
            : RedirectToAction(nameof(Manage));

    // Instructors land on their roster; coordinators on the full manage page.
    private IActionResult RedirectToEnrollmentList() =>
        User.IsInRole("Instructor")
            ? RedirectToAction(nameof(Roster))
            : RedirectToAction(nameof(Manage));

    private static int ActiveEnrollmentCount(CourseSession session) =>
        session.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped);

    private static decimal PaidTotal(Enrollment enrollment) =>
        enrollment.Payments?.Sum(p => p.AmountPaid) ?? 0m;

    private static decimal OutstandingBalance(Enrollment enrollment) =>
        Math.Max(0m, (enrollment.CourseSession?.Course?.EnrollmentFee ?? 0m) - PaidTotal(enrollment));

    // A payment is overdue once the session has started and they still owe money. Dropped ones don't count.
    private static bool IsOverdue(Enrollment enrollment)
    {
        if (enrollment.Status == EnrollmentStatus.Dropped) return false;
        if (OutstandingBalance(enrollment) <= 0) return false;
        var start = enrollment.CourseSession?.StartDateTime;
        return start.HasValue && start.Value <= DateTime.UtcNow;
    }

    private static string PaymentStatus(Enrollment enrollment)
    {
        var balance = OutstandingBalance(enrollment);
        if (balance <= 0) return "Paid";
        if (IsOverdue(enrollment)) return "Overdue";
        return PaidTotal(enrollment) > 0 ? "Partial" : "Unpaid";
    }

    // Sends one overdue reminder per enrollment. We tag the message so we don't send it twice, without changing the database.
    private async Task FlagOverduePaymentsAsync(IEnumerable<Enrollment> enrollments)
    {
        var overdue = enrollments.Where(IsOverdue).ToList();
        if (overdue.Count == 0) return;

        var markerIds = overdue.Select(e => $"[OVERDUE:{e.Id}]").ToList();
        var alreadyNotifiedMarkers = await _context.Notifications
            .Where(n => n.Type == "Payment" && markerIds.Any(m => n.Message.Contains(m)))
            .Select(n => n.Message)
            .ToListAsync();

        var alreadyNotified = new HashSet<int>(
            alreadyNotifiedMarkers
                .Select(ExtractEnrollmentIdMarker)
                .Where(id => id.HasValue)
                .Select(id => id!.Value));

        var newlyOverdue = overdue.Where(e => !alreadyNotified.Contains(e.Id)).ToList();
        if (newlyOverdue.Count == 0) return;

        foreach (var enrollment in newlyOverdue)
        {
            var balance = OutstandingBalance(enrollment);
            var courseTitle = enrollment.CourseSession?.Course?.Title ?? "your course";
            await AddNotificationForTraineeAsync(
                enrollment.TraineeId,
                $"Payment overdue for {courseTitle}: {balance:C} outstanding. [OVERDUE:{enrollment.Id}]",
                "Payment");
        }

        await SaveChangesAndNotifyAsync();
    }

    private static int? ExtractEnrollmentIdMarker(string message)
    {
        const string marker = "[OVERDUE:";
        var i = message.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return null;
        var start = i + marker.Length;
        var end = message.IndexOf(']', start);
        if (end < 0) return null;
        return int.TryParse(message.AsSpan(start, end - start), out var id) ? id : null;
    }

    private static string FullName(AppUser user) =>
        $"{user.FirstName} {user.LastName}".Trim();
}
