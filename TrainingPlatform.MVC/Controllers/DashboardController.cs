using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        var categoriesQuery = _db.CourseCategories
            .Select(c => new DashboardCategoryTab
            {
                Id = c.Id,
                Name = c.Name,
                CourseCount = c.Courses.Count
            });

        var coursesQuery = _db.Courses
            .Include(c => c.Category)
            .Include(c => c.Sessions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            coursesQuery = coursesQuery.Where(c => c.Title.Contains(search) || c.Description.Contains(search));

        if (categoryId.HasValue)
            coursesQuery = coursesQuery.Where(c => c.CategoryId == categoryId.Value);

        var courses = await coursesQuery
            .OrderBy(c => c.Title)
            .Take(9)
            .Select(c => new DashboardCourseCard
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CategoryName = c.Category.Name,
                Fee = c.EnrollmentFee,
                DurationHours = c.DurationHours,
                Capacity = c.Capacity,
                NextSessionStart = c.Sessions
                    .Where(s => s.StartDateTime > DateTime.UtcNow && s.Status == SessionStatus.Scheduled)
                    .OrderBy(s => s.StartDateTime)
                    .Select(s => (DateTime?)s.StartDateTime)
                    .FirstOrDefault(),
                NextSessionEnd = c.Sessions
                    .Where(s => s.StartDateTime > DateTime.UtcNow && s.Status == SessionStatus.Scheduled)
                    .OrderBy(s => s.StartDateTime)
                    .Select(s => (DateTime?)s.EndDateTime)
                    .FirstOrDefault(),
                AccentSeed = c.Title
            })
            .ToListAsync();

        var sessions = await BuildUpcomingSessionsAsync(user, role);
        var progress = await BuildLearningProgressAsync(user, role);
        var stats = await BuildStatsAsync(user, role);

        var model = new DashboardViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            Role = role,
            Categories = await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
            SelectedCategoryId = categoryId,
            Search = search,
            Courses = courses,
            UpcomingSessions = sessions,
            LearningProgress = progress,
            Stats = stats
        };

        return View(model);
    }

    private async Task<IReadOnlyList<DashboardSession>> BuildUpcomingSessionsAsync(AppUser user, string role)
    {
        var baseQuery = _db.CourseSessions
            .Include(s => s.Course).ThenInclude(c => c.Category)
            .Include(s => s.Instructor).ThenInclude(i => i.User)
            .Where(s => s.StartDateTime >= DateTime.UtcNow.AddDays(-1))
            .AsQueryable();

        if (role == "Trainee")
        {
            var trainee = await _db.Trainees.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (trainee is null) return [];
            baseQuery = baseQuery.Where(s => s.Enrollments.Any(e => e.TraineeId == trainee.Id));
        }
        else if (role == "Instructor")
        {
            var instructor = await _db.Instructors.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (instructor is null) return [];
            baseQuery = baseQuery.Where(s => s.InstructorId == instructor.Id);
        }

        return await baseQuery
            .OrderBy(s => s.StartDateTime)
            .Take(4)
            .Select(s => new DashboardSession
            {
                Id = s.Id,
                CourseId = s.CourseId,
                CourseTitle = s.Course.Title,
                InstructorName = s.Instructor.User.FirstName + " " + s.Instructor.User.LastName,
                CategoryName = s.Course.Category.Name,
                StartDateTime = s.StartDateTime,
                EndDateTime = s.EndDateTime,
                Status = s.Status
            })
            .ToListAsync();
    }

    private async Task<IReadOnlyList<DashboardProgressItem>> BuildLearningProgressAsync(AppUser user, string role)
    {
        var accents = new[] { "primary", "violet", "pink", "amber" };

        if (role == "Trainee")
        {
            var trainee = await _db.Trainees.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (trainee is null) return [];

            var certs = await _db.TraineeCertifications
                .Include(tc => tc.CertificationTrack).ThenInclude(t => t.CertificationTrackCourses)
                .Where(tc => tc.TraineeId == trainee.Id)
                .Take(4)
                .ToListAsync();

            var items = new List<DashboardProgressItem>();
            var i = 0;
            foreach (var cert in certs)
            {
                var required = cert.CertificationTrack.CertificationTrackCourses.Count;
                var completed = await _db.Enrollments
                    .Where(e => e.TraineeId == trainee.Id
                                && e.Status == EnrollmentStatus.Completed
                                && cert.CertificationTrack.CertificationTrackCourses.Select(tc => tc.CourseId).Contains(e.CourseSession.CourseId))
                    .CountAsync();
                var pct = cert.Status == CertificationStatus.Issued
                    ? 100
                    : required == 0 ? 0 : (int)Math.Round(100.0 * completed / required);
                items.Add(new DashboardProgressItem
                {
                    Label = cert.CertificationTrack.Name,
                    Sublabel = $"{completed}/{required} courses",
                    Percent = pct,
                    Accent = accents[i++ % accents.Length]
                });
            }
            return items;
        }

        if (role == "Instructor")
        {
            var instructor = await _db.Instructors.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (instructor is null) return [];

            var sessionsByCategory = await _db.CourseSessions
                .Include(s => s.Course).ThenInclude(c => c.Category)
                .Where(s => s.InstructorId == instructor.Id)
                .GroupBy(s => s.Course.Category.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Count(),
                    Completed = g.Count(s => s.Status == SessionStatus.Completed)
                })
                .Take(4)
                .ToListAsync();

            return sessionsByCategory
                .Select((g, idx) => new DashboardProgressItem
                {
                    Label = g.Category,
                    Sublabel = $"{g.Completed}/{g.Total} sessions",
                    Percent = g.Total == 0 ? 0 : (int)Math.Round(100.0 * g.Completed / g.Total),
                    Accent = accents[idx % accents.Length]
                })
                .ToList();
        }

        // Coordinator: enrollment fill rate per category (top 4 by activity)
        var coordinator = await _db.CourseCategories
            .Select(c => new
            {
                c.Name,
                Capacity = c.Courses.SelectMany(co => co.Sessions).Sum(s => (int?)s.Capacity) ?? 0,
                Enrolled = c.Courses.SelectMany(co => co.Sessions).Sum(s => s.Enrollments.Count)
            })
            .Where(x => x.Capacity > 0)
            .OrderByDescending(x => x.Enrolled)
            .Take(4)
            .ToListAsync();

        return coordinator
            .Select((g, idx) => new DashboardProgressItem
            {
                Label = g.Name,
                Sublabel = $"{g.Enrolled}/{g.Capacity} seats",
                Percent = g.Capacity == 0 ? 0 : (int)Math.Round(100.0 * g.Enrolled / g.Capacity),
                Accent = accents[idx % accents.Length]
            })
            .ToList();
    }

    private async Task<DashboardStats> BuildStatsAsync(AppUser user, string role)
    {
        var courseCount = await _db.Courses.CountAsync();
        var upcoming = await _db.CourseSessions
            .Where(s => s.StartDateTime >= DateTime.UtcNow && s.Status == SessionStatus.Scheduled)
            .CountAsync();

        if (role == "Trainee")
        {
            var trainee = await _db.Trainees.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (trainee is null) return new DashboardStats { CourseCount = courseCount, UpcomingSessionCount = upcoming };

            return new DashboardStats
            {
                CourseCount = courseCount,
                UpcomingSessionCount = upcoming,
                ActiveEnrollmentCount = await _db.Enrollments.CountAsync(e => e.TraineeId == trainee.Id
                                            && e.Status != EnrollmentStatus.Completed
                                            && e.Status != EnrollmentStatus.Dropped),
                CertificationCount = await _db.TraineeCertifications.CountAsync(c => c.TraineeId == trainee.Id)
            };
        }

        if (role == "Instructor")
        {
            var instructor = await _db.Instructors.FirstOrDefaultAsync(i => i.UserId == user.Id);
            if (instructor is null) return new DashboardStats { CourseCount = courseCount, UpcomingSessionCount = upcoming };

            return new DashboardStats
            {
                CourseCount = courseCount,
                UpcomingSessionCount = await _db.CourseSessions.CountAsync(s =>
                    s.InstructorId == instructor.Id && s.StartDateTime >= DateTime.UtcNow),
                ActiveEnrollmentCount = await _db.CourseSessions
                    .Where(s => s.InstructorId == instructor.Id)
                    .SelectMany(s => s.Enrollments)
                    .CountAsync(),
                CertificationCount = 0
            };
        }

        return new DashboardStats
        {
            CourseCount = courseCount,
            UpcomingSessionCount = upcoming,
            ActiveEnrollmentCount = await _db.Enrollments.CountAsync(e =>
                e.Status != EnrollmentStatus.Completed && e.Status != EnrollmentStatus.Dropped),
            CertificationCount = await _db.TraineeCertifications.CountAsync()
        };
    }
}
