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

        var viewModels = sessions.Select(s => new CourseSessionListItemViewModel
        {
            Id = s.Id,
            CourseTitle = s.Course.Title,
            InstructorName = $"{s.Instructor.User.FirstName} {s.Instructor.User.LastName}",
            ClassroomName = s.Classroom.Name,
            SessionDate = DateOnly.FromDateTime(s.StartDateTime),
            StartTime = TimeOnly.FromDateTime(s.StartDateTime),
            AvailableSpots = s.Capacity,
            EnrollmentCount = s.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped)
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

        return View(new CourseSessionDetailsViewModel
        {
            Id = session.Id,
            CourseTitle = session.Course.Title,
            CourseDescription = session.Course.Description,
            InstructorName = $"{session.Instructor.User.FirstName} {session.Instructor.User.LastName}",
            ClassroomName = session.Classroom.Name,
            ClassroomEquipment = string.Join(", ", session.Classroom.Equipment.Select(e => e.EquipmentName)),
            SessionDate = DateOnly.FromDateTime(session.StartDateTime),
            StartTime = TimeOnly.FromDateTime(session.StartDateTime),
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

        var startDateTime = model.SessionDate.ToDateTime(model.StartTime);

        var instructorConflict = await _db.CourseSessions.AnyAsync(s =>
            s.InstructorId == model.InstructorId &&
            s.StartDateTime == startDateTime);

        if (instructorConflict)
        {
            ModelState.AddModelError("InstructorId",
                "This instructor is already scheduled for another session at the same date and time.");
            return View(await BuildFormAsync(model));
        }

        var roomConflict = await _db.CourseSessions.AnyAsync(s =>
            s.ClassroomId == model.ClassroomId &&
            s.StartDateTime == startDateTime);

        if (roomConflict)
        {
            ModelState.AddModelError("ClassroomId",
                "This classroom is already booked for another session at the same date and time.");
            return View(await BuildFormAsync(model));
        }

        var classroom = await _db.Classrooms.FindAsync(model.ClassroomId);
        if (classroom != null && model.AvailableSpots > classroom.Capacity)
        {
            ModelState.AddModelError("AvailableSpots",
                $"Available spots cannot exceed the classroom capacity of {classroom.Capacity}.");
            return View(await BuildFormAsync(model));
        }

        var course = await _db.Courses.FindAsync(model.CourseId);
        var endDateTime = course != null
            ? startDateTime.AddHours(course.DurationHours)
            : startDateTime.AddHours(1);

        _db.CourseSessions.Add(new CourseSession
        {
            CourseId = model.CourseId,
            InstructorId = model.InstructorId,
            ClassroomId = model.ClassroomId,
            StartDateTime = startDateTime,
            EndDateTime = endDateTime,
            Capacity = model.AvailableSpots
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Course session scheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "TrainingCoordinator")]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await _db.CourseSessions.Include(s => s.Enrollments).FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();

        if (session.Enrollments.Any())
        {
            TempData["Error"] = "Cannot delete a session that has enrollments.";
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
    // - Trainee: sessions they're enrolled in (non-dropped)
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
            var trainee = await _db.Trainees.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (trainee is null) return query.Where(_ => false);
            return query.Where(s => s.Enrollments.Any(e =>
                e.TraineeId == trainee.Id && e.Status != EnrollmentStatus.Dropped));
        }

        return query.Where(_ => false);
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
