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

    public async Task<IActionResult> Index(int? courseId)
    {
        // Trainees now reach sessions through a specific course on the Courses page.
        if (User.IsInRole("Trainee") && courseId is null)
            return RedirectToAction("Index", "Courses");

        var query = _db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom)
            .Include(s => s.Enrollments)
            .AsQueryable();

        if (courseId is not null)
            query = query.Where(s => s.CourseId == courseId.Value);

        var scopedQuery = await ScopeQueryForCurrentUserAsync(query);

        if (scopedQuery is null) return Challenge();

        var sessions = await scopedQuery.OrderBy(s => s.StartDateTime).ToListAsync();

        // Track which sessions the trainee is in and which they dropped, since you can't rejoin a dropped one.
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

        if (courseId is not null)
        {
            ViewBag.FilterCourseId = courseId.Value;
            ViewBag.FilterCourseTitle = await _db.Courses
                .Where(c => c.Id == courseId.Value)
                .Select(c => c.Title)
                .FirstOrDefaultAsync();
        }

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
            CourseId = session.CourseId,
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

        if (!await InstructorIsAvailableAsync(model.InstructorId, startUtc, endUtc))
        {
            ModelState.AddModelError(nameof(model.InstructorId),
                "This instructor is not available on the selected day and time. Check their availability schedule.");
            return View(await BuildFormAsync(model));
        }

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

        if (!await InstructorIsAvailableAsync(model.InstructorId, startUtc, endUtc))
        {
            ModelState.AddModelError(nameof(model.InstructorId),
                "This instructor is not available on the selected day and time. Check their availability schedule.");
            return View(await BuildFormAsync(model));
        }

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

        // Only active enrollments stop a session from being deleted.
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

    // Limits the sessions to what each role should see. Returns null if we can't find the user.
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
            // Compare in UTC because that's how the start time is saved.
            var nowUtc = DateTime.UtcNow;
            return query.Where(s => s.Status == SessionStatus.Scheduled && s.StartDateTime > nowUtc);
        }

        return query.Where(_ => false);
    }

    // Turn the date and time from the form into UTC, since everything else compares in UTC.
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

    // Treat times from the database as UTC when showing them. Older rows might be off by the time zone.
    private static DateTime AsLocal(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return asUtc.ToLocalTime();
    }

    // Returns false if the instructor set availability that doesn't cover this slot. No availability set means they're always free.
    private async Task<bool> InstructorIsAvailableAsync(int instructorId, DateTime startUtc, DateTime endUtc)
    {
        var slots = await _db.InstructorAvailability
            .Where(a => a.InstructorId == instructorId)
            .ToListAsync();

        if (slots.Count == 0) return true; // no restrictions defined

        var localStart = AsLocal(startUtc);
        var localEnd   = AsLocal(endUtc);
        var day        = localStart.DayOfWeek;
        var startTime  = TimeOnly.FromDateTime(localStart);
        var endTime    = TimeOnly.FromDateTime(localEnd);

        return slots.Any(a =>
            a.DayOfWeek == day &&
            a.StartTime <= startTime &&
            a.EndTime   >= endTime);
    }

    // Two sessions clash if they run at the same time. Ignores the one being edited and cancelled ones.
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
