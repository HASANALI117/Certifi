using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.Hubs;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.Controllers
{
    public class EnrollmentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<EnrollmentHub> _hubContext;  // ← FIXED: single <

        public EnrollmentsController(AppDbContext context, IHubContext<EnrollmentHub> hubContext)  // ← FIXED: single <
        {
            _context = context;
            _hubContext = hubContext;
        }

        // ==========================================
        // GET: Enrollments/AvailableSessions (PUBLIC - for trainees)
        // ==========================================
        public async Task<IActionResult> AvailableSessions()
        {
            var sessions = await _context.CourseSessions
                .Include(cs => cs.Course)
                .Include(cs => cs.Instructor)
                    .ThenInclude(i => i.User)
                .Include(cs => cs.Classroom)
                .Include(cs => cs.Enrollments)
                .Where(cs => cs.StartDateTime > DateTime.Now
                    && cs.Status == SessionStatus.Scheduled)
                .Select(cs => new AvailableSessionViewModel
                {
                    Id = cs.Id,
                    CourseTitle = cs.Course.Title,
                    InstructorName = cs.Instructor.User.FirstName + " " + cs.Instructor.User.LastName,
                    ClassroomName = cs.Classroom.Name,
                    SessionDate = DateOnly.FromDateTime(cs.StartDateTime),
                    StartTime = TimeOnly.FromDateTime(cs.StartDateTime),
                    EndTime = TimeOnly.FromDateTime(cs.EndDateTime),
                    Capacity = cs.Capacity,
                    EnrolledCount = cs.Enrollments.Count,
                    AvailableSpots = cs.Capacity - cs.Enrollments.Count
                })
                .ToListAsync();

            return View(sessions);
        }

        // ==========================================
        // POST: Enrollments/Enroll (Trainee enrolls)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> Enroll(int courseSessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
            {
                TempData["Error"] = "Trainee profile not found.";
                return RedirectToAction(nameof(AvailableSessions));
            }

            var session = await _context.CourseSessions
                .Include(cs => cs.Enrollments)
                .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

            if (session == null)
            {
                TempData["Error"] = "Session not found.";
                return RedirectToAction(nameof(AvailableSessions));
            }

            if (session.Enrollments.Count >= session.Capacity)
            {
                TempData["Error"] = "This session is full.";
                return RedirectToAction(nameof(AvailableSessions));
            }

            if (await _context.Enrollments.AnyAsync(e =>
                e.TraineeId == trainee.Id && e.CourseSessionId == courseSessionId))
            {
                TempData["Error"] = "You are already enrolled.";
                return RedirectToAction(nameof(AvailableSessions));
            }

            var enrollment = new Enrollment
            {
                TraineeId = trainee.Id,
                CourseSessionId = courseSessionId,
                Status = EnrollmentStatus.Enrolled,
                EnrolledAt = DateTime.UtcNow
            };

            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();

            // SIGNALR BROADCAST
            await _hubContext.Clients.All.SendAsync("UpdateEnrollmentCount", courseSessionId);

            // Notification
            var notification = new Notification
            {
                UserId = userId,
                Message = $"Enrolled in {session.Course.Title}.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Enrollment successful!";
            return RedirectToAction(nameof(AvailableSessions));
        }

    }
}