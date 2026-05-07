using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Entities;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "Training Coordinator")]
public class CourseSessionsController : Controller
{
    private readonly TrainingPlatformDbContext _db;

    public CourseSessionsController(TrainingPlatformDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var sessions = await _db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom)
            .Include(s => s.Enrollments)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
            .Select(s => new CourseSessionListItemViewModel
            {
                Id = s.Id,
                CourseTitle = s.Course.Title,
                InstructorName = s.Instructor.User.FullName,
                ClassroomName = s.Classroom.Name,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                AvailableSpots = s.AvailableSpots,
                EnrollmentCount = s.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped)
            })
            .ToListAsync();

        return View(sessions);
    }

    public async Task<IActionResult> Details(int id)
    {
        var session = await _db.CourseSessions
            .Include(s => s.Course)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Include(s => s.Classroom)
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound();

        return View(new CourseSessionDetailsViewModel
        {
            Id = session.Id,
            CourseTitle = session.Course.Title,
            CourseDescription = session.Course.Description,
            InstructorName = session.Instructor.User.FullName,
            ClassroomName = session.Classroom.Name,
            ClassroomEquipment = session.Classroom.Equipment,
            SessionDate = session.SessionDate,
            StartTime = session.StartTime,
            AvailableSpots = session.AvailableSpots,
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

        // Validate instructor not double-booked on this date and time
        var instructorConflict = await _db.CourseSessions.AnyAsync(s =>
            s.InstructorId == model.InstructorId &&
            s.SessionDate == model.SessionDate &&
            s.StartTime == model.StartTime);

        if (instructorConflict)
        {
            ModelState.AddModelError("InstructorId",
                "This instructor is already scheduled for another session at the same date and time.");
            return View(await BuildFormAsync(model));
        }

        // Validate classroom not double-booked
        var roomConflict = await _db.CourseSessions.AnyAsync(s =>
            s.ClassroomId == model.ClassroomId &&
            s.SessionDate == model.SessionDate &&
            s.StartTime == model.StartTime);

        if (roomConflict)
        {
            ModelState.AddModelError("ClassroomId",
                "This classroom is already booked for another session at the same date and time.");
            return View(await BuildFormAsync(model));
        }

        // Validate available spots do not exceed classroom capacity
        var classroom = await _db.Classrooms.FindAsync(model.ClassroomId);
        if (classroom != null && model.AvailableSpots > classroom.Capacity)
        {
            ModelState.AddModelError("AvailableSpots",
                $"Available spots cannot exceed the classroom capacity of {classroom.Capacity}.");
            return View(await BuildFormAsync(model));
        }

        _db.CourseSessions.Add(new CourseSession
        {
            CourseId = model.CourseId,
            InstructorId = model.InstructorId,
            ClassroomId = model.ClassroomId,
            SessionDate = model.SessionDate,
            StartTime = model.StartTime,
            AvailableSpots = model.AvailableSpots
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
            .Include(i => i.User)
            .Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.User.FullName })
            .ToListAsync();

        model.Classrooms = await _db.Classrooms
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.Name} (cap. {c.Capacity})" })
            .ToListAsync();

        return model;
    }
}
