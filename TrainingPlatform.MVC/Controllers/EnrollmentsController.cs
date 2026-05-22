using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.Hubs;
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

    [AllowAnonymous]
    public async Task<IActionResult> AvailableSessions()
    {
        var sessions = await _context.CourseSessions
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor).ThenInclude(i => i.User)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.Enrollments)
            .Where(cs => cs.StartDateTime > DateTime.Now && cs.Status == SessionStatus.Scheduled)
            .OrderBy(cs => cs.StartDateTime)
            .Select(cs => new AvailableSessionViewModel
            {
                Id = cs.Id,
                CourseTitle = cs.Course.Title,
                InstructorName = cs.Instructor.User.FirstName + " " + cs.Instructor.User.LastName,
                ClassroomName = cs.Classroom.Name,
                SessionDate = DateOnly.FromDateTime(cs.StartDateTime),
                StartTime = TimeOnly.FromDateTime(cs.StartDateTime),
                EndTime = TimeOnly.FromDateTime(cs.EndDateTime),
                Fee = cs.Course.EnrollmentFee,
                Capacity = cs.Capacity,
                EnrolledCount = cs.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped),
                AvailableSpots = cs.Capacity - cs.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped)
            })
            .ToListAsync();

        return View(sessions);
    }

    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Index()
    {
        var enrollments = await EnrollmentQuery()
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        return View(enrollments);
    }

    [Authorize(Roles = "TrainingCoordinator,Instructor")]
    public async Task<IActionResult> Manage()
    {
        var enrollments = await EnrollmentQuery()
            .OrderBy(e => e.CourseSession.StartDateTime)
            .ThenBy(e => e.Trainee.User.LastName)
            .ToListAsync();

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
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        return enrollment == null ? NotFound() : View(enrollment);
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await SetEnrollmentSelectListsAsync();
        return View(new Enrollment { EnrolledAt = DateTime.Now });
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TraineeId,CourseSessionId,Status,EnrolledAt")] Enrollment enrollment)
    {
        ClearEnrollmentNavigationValidation();

        if (!ModelState.IsValid)
        {
            await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
            return View(enrollment);
        }

        var session = await _context.CourseSessions
            .Include(cs => cs.Course)
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == enrollment.CourseSessionId);

        if (session == null)
        {
            ModelState.AddModelError(nameof(enrollment.CourseSessionId), "Selected course session was not found.");
            await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
            return View(enrollment);
        }

        if (ActiveEnrollmentCount(session) >= session.Capacity)
        {
            ModelState.AddModelError(nameof(enrollment.CourseSessionId), "This course session is full.");
            await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
            return View(enrollment);
        }

        var duplicate = await _context.Enrollments.AnyAsync(e =>
            e.TraineeId == enrollment.TraineeId &&
            e.CourseSessionId == enrollment.CourseSessionId &&
            e.Status != EnrollmentStatus.Dropped);

        if (duplicate)
        {
            ModelState.AddModelError(nameof(enrollment.TraineeId), "This trainee is already actively enrolled in the selected session.");
            await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
            return View(enrollment);
        }

        enrollment.EnrolledAt = enrollment.EnrolledAt == default ? DateTime.UtcNow : enrollment.EnrolledAt;
        _context.Enrollments.Add(enrollment);

        await StartCertificationTrackingAsync(enrollment.TraineeId, session.CourseId);
        await AddNotificationForTraineeAsync(enrollment.TraineeId, $"You were enrolled in {session.Course.Title}.", "Enrollment");
        await SaveChangesAndNotifyAsync();
        await BroadcastEnrollmentCountAsync(enrollment.CourseSessionId);

        TempData["Success"] = "Enrollment created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null) return NotFound();

        await SetEnrollmentSelectListsAsync(enrollment.TraineeId, enrollment.CourseSessionId);
        return View(enrollment);
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TraineeId,CourseSessionId,Status,EnrolledAt")] Enrollment model)
    {
        if (id != model.Id) return NotFound();

        ClearEnrollmentNavigationValidation();

        if (!ModelState.IsValid)
        {
            await SetEnrollmentSelectListsAsync(model.TraineeId, model.CourseSessionId);
            return View(model);
        }

        var session = await _context.CourseSessions
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == model.CourseSessionId);

        if (session == null)
        {
            ModelState.AddModelError(nameof(model.CourseSessionId), "Selected course session was not found.");
            await SetEnrollmentSelectListsAsync(model.TraineeId, model.CourseSessionId);
            return View(model);
        }

        var duplicate = await _context.Enrollments.AnyAsync(e =>
            e.Id != id &&
            e.TraineeId == model.TraineeId &&
            e.CourseSessionId == model.CourseSessionId &&
            e.Status != EnrollmentStatus.Dropped);

        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.TraineeId), "This trainee is already actively enrolled in the selected session.");
            await SetEnrollmentSelectListsAsync(model.TraineeId, model.CourseSessionId);
            return View(model);
        }

        var activeEnrollmentCount = session.Enrollments.Count(e => e.Id != id && e.Status != EnrollmentStatus.Dropped);
        if (model.Status != EnrollmentStatus.Dropped && activeEnrollmentCount >= session.Capacity)
        {
            ModelState.AddModelError(nameof(model.CourseSessionId), "This course session is full.");
            await SetEnrollmentSelectListsAsync(model.TraineeId, model.CourseSessionId);
            return View(model);
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

        TempData["Success"] = "Enrollment updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == id);
        return enrollment == null ? NotFound() : View(enrollment);
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

        TempData["Success"] = "Enrollment deleted.";
        return RedirectToAction(nameof(Index));
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
            return RedirectToAction(nameof(AvailableSessions));
        }

        var session = await _context.CourseSessions
            .Include(cs => cs.Course)
            .Include(cs => cs.Enrollments)
            .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

        if (session == null)
        {
            TempData["Error"] = "Session not found.";
            return RedirectToAction(nameof(AvailableSessions));
        }

        if (ActiveEnrollmentCount(session) >= session.Capacity)
        {
            TempData["Error"] = "This session is full.";
            return RedirectToAction(nameof(AvailableSessions));
        }

        var existingEnrollment = await _context.Enrollments.FirstOrDefaultAsync(e =>
            e.TraineeId == trainee.Id && e.CourseSessionId == courseSessionId);

        if (existingEnrollment is { Status: not EnrollmentStatus.Dropped })
        {
            TempData["Error"] = "You are already enrolled.";
            return RedirectToAction(nameof(AvailableSessions));
        }

        if (existingEnrollment == null)
        {
            existingEnrollment = new Enrollment
            {
                TraineeId = trainee.Id,
                CourseSessionId = courseSessionId,
                Status = EnrollmentStatus.Enrolled,
                EnrolledAt = DateTime.UtcNow
            };
            _context.Enrollments.Add(existingEnrollment);
        }
        else
        {
            existingEnrollment.Status = EnrollmentStatus.Enrolled;
            existingEnrollment.EnrolledAt = DateTime.UtcNow;
        }

        await StartCertificationTrackingAsync(trainee.Id, session.CourseId);
        AddNotification(userId, $"Enrolled in {session.Course.Title}.", "Enrollment");
        await SaveChangesAndNotifyAsync();
        await BroadcastEnrollmentCountAsync(courseSessionId);

        TempData["Success"] = "Enrollment successful.";
        return RedirectToAction(nameof(AvailableSessions));
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

    [Authorize(Roles = "TrainingCoordinator,Trainee")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int enrollmentId, decimal amount)
    {
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == enrollmentId);
        if (enrollment == null) return NotFound();

        if (User.IsInRole("Trainee") && !await CurrentUserOwnsEnrollmentAsync(enrollmentId))
            return Forbid();

        if (enrollment.Status == EnrollmentStatus.Dropped)
        {
            TempData["Error"] = "Dropped enrollments cannot accept payments.";
            return RedirectAfterPayment();
        }

        var remainingBalance = OutstandingBalance(enrollment);
        if (amount <= 0 || amount > remainingBalance)
        {
            TempData["Error"] = "Payment amount must be greater than zero and no more than the outstanding balance.";
            return RedirectAfterPayment();
        }

        var newBalance = remainingBalance - amount;
        _context.Payments.Add(new Payment
        {
            EnrollmentId = enrollmentId,
            AmountPaid = amount,
            PaidAt = DateTime.UtcNow,
            OutstandingBalance = newBalance
        });

        await AddNotificationForTraineeAsync(
            enrollment.TraineeId,
            $"Payment of {amount:C} recorded for {enrollment.CourseSession.Course.Title}. Remaining balance: {newBalance:C}.",
            "Payment");

        var confirmedByPayment = newBalance <= 0 && enrollment.Status == EnrollmentStatus.Enrolled;
        if (confirmedByPayment)
        {
            enrollment.Status = EnrollmentStatus.Confirmed;
            await AddNotificationForTraineeAsync(
                enrollment.TraineeId,
                $"Your enrollment for {enrollment.CourseSession.Course.Title} is confirmed after full payment.",
                "Enrollment");
        }

        await SaveChangesAndNotifyAsync();

        TempData["Success"] = confirmedByPayment
            ? "Payment recorded. Balance is fully paid and the enrollment is confirmed."
            : newBalance == 0 ? "Payment recorded. Balance is fully paid." : "Payment recorded.";
        return RedirectAfterPayment();
    }

    [Authorize(Roles = "TrainingCoordinator,Instructor")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordAssessment(int enrollmentId, AssessmentResult result, string? notes)
    {
        var enrollment = await EnrollmentQuery().FirstOrDefaultAsync(e => e.Id == enrollmentId);
        if (enrollment == null) return NotFound();

        var recordedById = await GetAssessmentRecorderIdAsync(enrollment);
        if (recordedById == null)
        {
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

        if (result == AssessmentResult.Pass)
        {
            await UpdateCertificationTrackingAsync(enrollment.TraineeId);
            await SaveChangesAndNotifyAsync();
        }

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
        certification.CertRefNumber = string.IsNullOrWhiteSpace(certification.CertRefNumber)
            ? BuildCertificateReference(certification)
            : certification.CertRefNumber;

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

            if (requiredCourseIds.Count == 0 || !requiredCourseIds.Any(passedCourseIds.Contains))
                continue;

            var certification = await _context.TraineeCertifications
                .FirstOrDefaultAsync(c => c.TraineeId == traineeId && c.CertificationTrackId == track.Id);

            if (certification == null)
            {
                certification = new TraineeCertification
                {
                    TraineeId = traineeId,
                    CertificationTrackId = track.Id,
                    Status = CertificationStatus.InProgress
                };
                _context.TraineeCertifications.Add(certification);
            }

            if (requiredCourseIds.All(passedCourseIds.Contains) && certification.Status == CertificationStatus.InProgress)
            {
                certification.Status = CertificationStatus.Eligible;
                await AddNotificationForTraineeAsync(
                    traineeId,
                    $"You are eligible for certification: {track.Name}.",
                    "Certification");
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

    private static string PaymentStatus(Enrollment enrollment)
    {
        var balance = OutstandingBalance(enrollment);
        if (balance <= 0) return "Paid";
        return PaidTotal(enrollment) > 0 ? "Partial" : "Unpaid";
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
