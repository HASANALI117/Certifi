using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize]
public class CourseSessionsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public CourseSessionsController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var scopedQuery = await ScopeQueryForCurrentUserAsync(_db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom)
            .Include(s => s.Enrollments)
            .AsQueryable());

        if (scopedQuery is null) return Challenge();

        var sessions = await scopedQuery.OrderBy(s => s.StartDateTime).ToListAsync();

        // Sessions the current trainee is actively enrolled in (drives Enroll vs Enrolled state),
        // and sessions they were dropped from (a dropped session is not re-joinable — they must
        // pick a different upcoming session of the course).
        var enrolledSessionIds = new HashSet<int>();
        var droppedSessionIds = new HashSet<int>();
        if (User.IsInRole("Trainee"))
        {
            var user = await _userManager.GetUserAsync(User);
            var trainee = user is null ? null : await _db.Trainees.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (trainee != null)
            {
                var rows = await _db.Enrollments
                    .Where(e => e.TraineeId == trainee.Id)
                    .Select(e => new { e.CourseSessionId, e.Status })
                    .ToListAsync();
                enrolledSessionIds = rows.Where(r => r.Status != EnrollmentStatus.Dropped).Select(r => r.CourseSessionId).ToHashSet();
                droppedSessionIds = rows.Where(r => r.Status == EnrollmentStatus.Dropped).Select(r => r.CourseSessionId).ToHashSet();
            }
        }

        var viewModels = sessions.Select(s =>
        {
            var local = AsLocal(s.StartDateTime);
            return new CourseSessionListItemViewModel
            {
                Id = s.Id,
                CourseTitle = s.Course.Title,
                InstructorName = $"{s.Instructor.User.FirstName} {s.Instructor.User.LastName}",
                ClassroomName = s.Classroom.Name,
                SessionDate = DateOnly.FromDateTime(local),
                StartTime = TimeOnly.FromDateTime(local),
                Capacity = s.Capacity,
                AvailableSpots = s.Capacity,
                EnrollmentCount = s.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped),
                Fee = s.Course.EnrollmentFee,
                IsEnrolledByCurrentUser = enrolledSessionIds.Contains(s.Id),
                WasDroppedByCurrentUser = droppedSessionIds.Contains(s.Id)
            };
        }).ToList();

        return View(viewModels);
    }

    public async Task<IActionResult> Details(int id)
    {
        var scopedQuery = await ScopeQueryForCurrentUserAsync(_db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom).ThenInclude(c => c.Equipment)
            .Include(s => s.Enrollments)
            .AsQueryable());

        if (scopedQuery is null) return Challenge();

        var session = await scopedQuery.FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound();

        var local = AsLocal(session.StartDateTime);

        return View(new CourseSessionDetailsViewModel
        {
            Id = session.Id,
            CourseTitle = session.Course.Title,
            CourseDescription = session.Course.Description,
            InstructorName = $"{session.Instructor.User.FirstName} {session.Instructor.User.LastName}",
            ClassroomName = session.Classroom.Name,
            ClassroomEquipment = string.Join(", ", session.Classroom.Equipment.Select(e => e.EquipmentName)),
            SessionDate = DateOnly.FromDateTime(local),
            StartTime = TimeOnly.FromDateTime(local),
            AvailableSpots = session.Capacity,
            EnrollmentCount = session.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped)
        });
    }

    [HttpGet]
    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Create()
    {
        return View(await BuildFormAsync(null));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Create(CourseSessionFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildFormAsync(model));

        var course = await _db.Courses.FindAsync(model.CourseId);
        if (course == null)
        {
            ModelState.AddModelError(nameof(model.CourseId), "Selected course was not found.");
            return View(await BuildFormAsync(model));
        }

        var (startUtc, endUtc) = ResolveSessionWindow(model, course);

        if (await HasInstructorOverlapAsync(model.InstructorId, startUtc, endUtc, excludingSessionId: null))
        {
            ModelState.AddModelError(nameof(model.InstructorId),
                "This instructor is already scheduled for an overlapping session.");
            return View(await BuildFormAsync(model));
        }

        if (await HasClassroomOverlapAsync(model.ClassroomId, startUtc, endUtc, excludingSessionId: null))
        {
            ModelState.AddModelError(nameof(model.ClassroomId),
                "This classroom is already booked for an overlapping session.");
            return View(await BuildFormAsync(model));
        }

        var classroom = await _db.Classrooms.FindAsync(model.ClassroomId);
        if (classroom != null && model.AvailableSpots > classroom.Capacity)
        {
            ModelState.AddModelError(nameof(model.AvailableSpots),
                $"Available spots cannot exceed the classroom capacity of {classroom.Capacity}.");
            return View(await BuildFormAsync(model));
        }

        _db.CourseSessions.Add(new CourseSession
        {
            CourseId = model.CourseId,
            InstructorId = model.InstructorId,
            ClassroomId = model.ClassroomId,
            StartDateTime = startUtc,
            EndDateTime = endUtc,
            Capacity = model.AvailableSpots
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Course session scheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Edit(int id)
    {
        var session = await _db.CourseSessions.FindAsync(id);
        if (session == null) return NotFound();

        var local = AsLocal(session.StartDateTime);
        var model = new CourseSessionFormViewModel
        {
            Id = session.Id,
            CourseId = session.CourseId,
            InstructorId = session.InstructorId,
            ClassroomId = session.ClassroomId,
            SessionDate = DateOnly.FromDateTime(local),
            StartTime = TimeOnly.FromDateTime(local),
            AvailableSpots = session.Capacity
        };
        return View(await BuildFormAsync(model));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Edit(CourseSessionFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildFormAsync(model));

        var session = await _db.CourseSessions
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == model.Id);
        if (session == null) return NotFound();

        var course = await _db.Courses.FindAsync(model.CourseId);
        if (course == null)
        {
            ModelState.AddModelError(nameof(model.CourseId), "Selected course was not found.");
            return View(await BuildFormAsync(model));
        }

        var (startUtc, endUtc) = ResolveSessionWindow(model, course);

        if (await HasInstructorOverlapAsync(model.InstructorId, startUtc, endUtc, excludingSessionId: session.Id))
        {
            ModelState.AddModelError(nameof(model.InstructorId),
                "This instructor is already scheduled for an overlapping session.");
            return View(await BuildFormAsync(model));
        }

        if (await HasClassroomOverlapAsync(model.ClassroomId, startUtc, endUtc, excludingSessionId: session.Id))
        {
            ModelState.AddModelError(nameof(model.ClassroomId),
                "This classroom is already booked for an overlapping session.");
            return View(await BuildFormAsync(model));
        }

        var classroom = await _db.Classrooms.FindAsync(model.ClassroomId);
        if (classroom != null && model.AvailableSpots > classroom.Capacity)
        {
            ModelState.AddModelError(nameof(model.AvailableSpots),
                $"Available spots cannot exceed the classroom capacity of {classroom.Capacity}.");
            return View(await BuildFormAsync(model));
        }

        var activeEnrollmentCount = session.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped);
        if (model.AvailableSpots < activeEnrollmentCount)
        {
            ModelState.AddModelError(nameof(model.AvailableSpots),
                $"Capacity cannot drop below the {activeEnrollmentCount} active enrollment(s) on this session.");
            return View(await BuildFormAsync(model));
        }

        session.CourseId = model.CourseId;
        session.InstructorId = model.InstructorId;
        session.ClassroomId = model.ClassroomId;
        session.StartDateTime = startUtc;
        session.EndDateTime = endUtc;
        session.Capacity = model.AvailableSpots;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Course session updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await _db.CourseSessions.Include(s => s.Enrollments).FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();

        // Dropped enrollments are historical and shouldn't keep a session
        // around. Only active (non-dropped) enrollments should block deletion.
        if (session.Enrollments.Any(e => e.Status != EnrollmentStatus.Dropped))
        {
            TempData["Error"] = "Cannot delete a session that has active enrollments.";
            return RedirectToAction(nameof(Index));
        }

        _db.CourseSessions.Remove(session);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Session deleted.";
        return RedirectToAction(nameof(Index));
    }

    // Filters the session query so each role only sees their own sessions:
    // - Coordinator: every session
    // - Instructor: sessions they teach
    // - Trainee: every upcoming scheduled session (browse + enroll surface)
    // Returns null when the signed-in user record can't be resolved.
    private async Task<IQueryable<CourseSession>?> ScopeQueryForCurrentUserAsync(IQueryable<CourseSession> query)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return null;

        if (User.IsInRole("TrainingCoordinator"))
        {
            return query;
        }

        if (User.IsInRole("Instructor"))
        {
            var instructor = await _db.Instructors.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (instructor is null) return query.Where(_ => false);
            return query.Where(s => s.InstructorId == instructor.Id);
        }

        if (User.IsInRole("Trainee"))
        {
            // Trainees browse all upcoming scheduled sessions. Compared in UTC
            // because StartDateTime is now stored as UTC by Create/Edit.
            var nowUtc = DateTime.UtcNow;
            return query.Where(s => s.Status == SessionStatus.Scheduled && s.StartDateTime > nowUtc);
        }

        return query.Where(_ => false);
    }

    // Converts a Date + Time pair entered by the coordinator (interpreted as
    // the server's local time zone, since the form has no TZ picker) into a
    // UTC pair. All comparisons elsewhere use DateTime.UtcNow, so storing UTC
    // keeps the pipeline consistent.
    private static (DateTime StartUtc, DateTime EndUtc) ResolveSessionWindow(
        CourseSessionFormViewModel model, Course course)
    {
        var localStart = DateTime.SpecifyKind(
            model.SessionDate.ToDateTime(model.StartTime),
            DateTimeKind.Local);
        var startUtc = localStart.ToUniversalTime();
        var endUtc = startUtc.AddHours(course.DurationHours > 0 ? course.DurationHours : 1);
        return (startUtc, endUtc);
    }

    // Treat any DateTime read from EF (Kind = Unspecified) as UTC for display.
    // The seeder and the Create/Edit pipeline both store UTC, so this is safe;
    // legacy rows written before this fix may be off by the server's TZ offset.
    private static DateTime AsLocal(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return asUtc.ToLocalTime();
    }

    // Two sessions overlap when neither one finishes before the other starts.
    // Excludes the row being edited (so reschedules don't conflict with themselves).
    // Excludes Cancelled sessions — they no longer occupy the slot.
    private async Task<bool> HasInstructorOverlapAsync(int instructorId, DateTime startUtc, DateTime endUtc, int? excludingSessionId)
    {
        return await _db.CourseSessions.AnyAsync(s =>
            s.InstructorId == instructorId &&
            s.Status != SessionStatus.Cancelled &&
            (excludingSessionId == null || s.Id != excludingSessionId.Value) &&
            s.StartDateTime < endUtc && s.EndDateTime > startUtc);
    }

    private async Task<bool> HasClassroomOverlapAsync(int classroomId, DateTime startUtc, DateTime endUtc, int? excludingSessionId)
    {
        return await _db.CourseSessions.AnyAsync(s =>
            s.ClassroomId == classroomId &&
            s.Status != SessionStatus.Cancelled &&
            (excludingSessionId == null || s.Id != excludingSessionId.Value) &&
            s.StartDateTime < endUtc && s.EndDateTime > startUtc);
    }

    private async Task<CourseSessionFormViewModel> BuildFormAsync(CourseSessionFormViewModel? existing)
    {
        var model = existing ?? new CourseSessionFormViewModel();

        model.Courses = await _db.Courses
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
            .ToListAsync();

        model.Instructors = await _db.Instructors
            .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.User.FirstName + " " + i.User.LastName })
            .ToListAsync();

        model.Classrooms = await _db.Classrooms
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.Name} (cap. {c.Capacity})" })
            .ToListAsync();

        return model;
    }
}
