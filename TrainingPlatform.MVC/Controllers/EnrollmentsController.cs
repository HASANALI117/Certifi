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

    // Index/Create/Edit/Delete/Details no longer have dedicated pages — everything
    // is managed from the Manage page via the reusable popup component.
    [Authorize(Roles = "TrainingCoordinator")]
    public IActionResult Index() => RedirectToAction(nameof(Manage));

    [Authorize(Roles = "TrainingCoordinator,Instructor")]
    public async Task<IActionResult> Manage()
    {
        var enrollments = await EnrollmentQuery()
            .OrderBy(e => e.CourseSession.StartDateTime)
            .ThenBy(e => e.Trainee.User.LastName)
            .ToListAsync();

        await FlagOverduePaymentsAsync(enrollments);
        ViewBag.PaymentStatuses = enrollments.ToDictionary(e => e.Id, PaymentStatus);

        await LoadCertificationViewBagAsync();
        return View(enrollments);
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
            ViewBag.PaymentStatuses = new Dictionary<int, string>();
            ViewBag.OutstandingBalances = new Dictionary<int, decimal>();
            ViewBag.Notifications = Enumerable.Empty<Notification>();
            ViewBag.Certifications = Enumerable.Empty<TraineeCertification>();
            return View(Enumerable.Empty<Enrollment>());
        }

        var enrollments = await EnrollmentQuery()
            .Where(e => e.TraineeId == trainee.Id)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        await FlagOverduePaymentsAsync(enrollments);

        ViewBag.PaymentStatuses = enrollments.ToDictionary(e => e.Id, PaymentStatus);
        ViewBag.OutstandingBalances = enrollments.ToDictionary(e => e.Id, OutstandingBalance);
        ViewBag.Notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync();
        ViewBag.Certifications = await _context.TraineeCertifications
            .Include(c => c.CertificationTrack)
            .Where(c => c.TraineeId == trainee.Id)
            .OrderBy(c => c.CertificationTrack.Name)
            .ToListAsync();

        return View(enrollments);
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

        // Wrap read-check-insert in a serializable transaction so that two
        // simultaneous enrollments racing for the last seat can't both succeed.
        // Under serializable isolation, SQL Server takes range locks on the
        // session's enrollment rows, so the second transaction blocks until the
        // first commits and then re-evaluates against the updated count.
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

        // A session is a one-shot, time-bound instance: only an upcoming, Scheduled
        // session is open for enrollment. The browse list already hides others; this
        // closes the direct-POST hole.
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

        // (TraineeId, CourseSessionId) is unique. A dropped session is not re-joinable —
        // the trainee must enroll in a different upcoming session of the course instead.
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

    // Trainees self-pay via Stripe Checkout (PaymentsController). This just renders the
    // amount-entry modal; the actual charge is handled by Stripe + the webhook.
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
        if (!IsModal) return RedirectToAction(nameof(Manage));
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        if (enrollment == null) return NotFound();
        return PartialView("_AssessmentForm", enrollment);
    }

    // Manual/offline payment recording has been removed — all payments go through
    // Stripe Checkout (see PaymentsController). The Stripe webhook is the single
    // writer of Payment rows.

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
            return RedirectToAction(nameof(Manage));
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

        // Always re-run tracking. UpdateCertificationTrackingAsync now handles
        // both promotion (Eligible when all required courses passed) and
        // demotion (back to InProgress when a previous Pass is corrected to
        // Fail). Already-Issued certifications are intentionally left alone —
        // a real-world certificate that has been handed out cannot be silently
        // revoked from the platform.
        await UpdateCertificationTrackingAsync(enrollment.TraineeId);
        await SaveChangesAndNotifyAsync();

        if (IsModal) return JsonOk("Assessment recorded.", "Assessment");
        TempData["Success"] = "Assessment recorded.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueCertification(int certificationId)
    {
        var certification = await _context.TraineeCertifications
            .Include(c => c.Trainee)
            .Include(c => c.CertificationTrack)
            .FirstOrDefaultAsync(c => c.Id == certificationId);

        if (certification == null) return NotFound();

        certification.Status = CertificationStatus.Issued;
        certification.IssuedAt = DateTime.UtcNow;

        // Persist the issued state first so the reference embeds a saved row's
        // Id (collision-free across coordinators issuing concurrently). On the
        // happy path the row already had an Id from StartCertificationTrackingAsync,
        // but using SaveChangesAsync as the anchor keeps the invariant intact
        // even if the upstream creation flow changes.
        var generateRef = string.IsNullOrWhiteSpace(certification.CertRefNumber);
        if (generateRef) await _context.SaveChangesAsync();
        if (generateRef) certification.CertRefNumber = BuildCertificateReference(certification);

        await AddNotificationForTraineeAsync(
            certification.TraineeId,
            $"Certification issued: {certification.CertificationTrack.Name}. Reference: {certification.CertRefNumber}.",
            "Certification");

        await SaveChangesAndNotifyAsync();

        TempData["Success"] = "Certification issued.";
        return RedirectToAction(nameof(Manage));
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

    private async Task LoadCertificationViewBagAsync()
    {
        ViewBag.Certifications = await _context.TraineeCertifications
            .Include(c => c.Trainee).ThenInclude(t => t.User)
            .Include(c => c.CertificationTrack)
            .OrderBy(c => c.Status)
            .ThenBy(c => c.CertificationTrack.Name)
            .ToListAsync();
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

            // Demotion: a prior Eligible cert no longer qualifies (e.g., a Pass
            // was corrected to Fail). Move it back to InProgress and notify so
            // the coordinator knows not to issue.
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

    private static int ActiveEnrollmentCount(CourseSession session) =>
        session.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped);

    private static decimal PaidTotal(Enrollment enrollment) =>
        enrollment.Payments?.Sum(p => p.AmountPaid) ?? 0m;

    private static decimal OutstandingBalance(Enrollment enrollment) =>
        Math.Max(0m, (enrollment.CourseSession?.Course?.EnrollmentFee ?? 0m) - PaidTotal(enrollment));

    // Overdue when the session has already started but the trainee still owes.
    // Dropped enrollments never count as overdue.
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

    // Idempotently sends a one-time "payment overdue" notification per
    // enrollment. Identifies prior notifications by an embedded marker in
    // the message body so we don't need a schema migration.
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

    private static string BuildCertificateReference(TraineeCertification certification)
    {
        var prefix = string.IsNullOrWhiteSpace(certification.CertificationTrack.CertRefPrefix)
            ? "CERT"
            : certification.CertificationTrack.CertRefPrefix.Trim();

        return $"{prefix}-{DateTime.UtcNow:yyyy}-{certification.Id:D5}";
    }
}
