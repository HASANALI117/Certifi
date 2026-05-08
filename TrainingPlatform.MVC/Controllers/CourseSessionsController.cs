using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "TrainingCoordinator")]
public class CourseSessionsController : Controller
{
    private readonly AppDbContext _db;

    public CourseSessionsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var sessions = await _db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom)
            .Include(s => s.Enrollments)
            .OrderBy(s => s.StartDateTime)
            .ToListAsync();

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
        var session = await _db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom).ThenInclude(c => c.Equipment)
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id);

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
    public async Task<IActionResult> Create()
    {
        return View(await BuildFormAsync(null));
    }

    [HttpPost, ValidateAntiForgeryToken]
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
