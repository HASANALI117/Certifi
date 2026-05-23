using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.DTOs;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "TrainingCoordinator")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    // GET /api/reports/overview
    [HttpGet("overview")]
    public async Task<ActionResult<ReportOverviewDto>> Overview()
    {
        var dto = new ReportOverviewDto
        {
            TotalCourses = await db.Courses.CountAsync(),
            TotalSessions = await db.CourseSessions.CountAsync(),
            TotalTrainees = await db.Trainees.CountAsync(),
            TotalInstructors = await db.Instructors.CountAsync(),
            ActiveEnrollments = await db.Enrollments.CountAsync(e =>
                e.Status == EnrollmentStatus.Enrolled
                || e.Status == EnrollmentStatus.Confirmed
                || e.Status == EnrollmentStatus.Attending),
            CompletedEnrollments = await db.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Completed),
            CertificatesIssued = await db.TraineeCertifications.CountAsync(c => c.Status == CertificationStatus.Issued),
            TotalRevenue = await db.Payments.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m
        };

        // Outstanding = latest balance per non-dropped enrollment, or full fee if unpaid.
        var balances = await db.Enrollments
            .Where(e => e.Status != EnrollmentStatus.Dropped)
            .Select(e => new
            {
                Fee = e.CourseSession.Course.EnrollmentFee,
                Latest = e.Payments.OrderByDescending(p => p.PaidAt)
                    .Select(p => (decimal?)p.OutstandingBalance).FirstOrDefault()
            })
            .ToListAsync();
        dto.OutstandingRevenue = balances.Sum(b => b.Latest ?? b.Fee);

        return Ok(dto);
    }

    // GET /api/reports/enrollments
    [HttpGet("enrollments")]
    public async Task<ActionResult<IEnumerable<EnrollmentStatReportDto>>> Enrollments()
    {
        var data = await db.Courses
            .AsNoTracking()
            .Select(c => new EnrollmentStatReportDto
            {
                CourseId = c.Id,
                CourseTitle = c.Title,
                CategoryName = c.Category.Name,
                TotalEnrollments = c.Sessions.SelectMany(s => s.Enrollments).Count(),
                EnrolledCount = c.Sessions.SelectMany(s => s.Enrollments).Count(e => e.Status == EnrollmentStatus.Enrolled),
                ConfirmedCount = c.Sessions.SelectMany(s => s.Enrollments).Count(e => e.Status == EnrollmentStatus.Confirmed),
                AttendingCount = c.Sessions.SelectMany(s => s.Enrollments).Count(e => e.Status == EnrollmentStatus.Attending),
                CompletedCount = c.Sessions.SelectMany(s => s.Enrollments).Count(e => e.Status == EnrollmentStatus.Completed),
                DroppedCount = c.Sessions.SelectMany(s => s.Enrollments).Count(e => e.Status == EnrollmentStatus.Dropped)
            })
            .ToListAsync();

        return Ok(data);
    }

    // GET /api/reports/instructors
    [HttpGet("instructors")]
    public async Task<ActionResult<IEnumerable<InstructorWorkloadDto>>> Instructors()
    {
        var rows = await db.Instructors
            .AsNoTracking()
            .Select(i => new
            {
                i.Id,
                Name = i.User.FirstName + " " + i.User.LastName,
                SessionCount = i.CourseSessions.Count,
                Sessions = i.CourseSessions.Select(s => new { s.StartDateTime, s.EndDateTime }).ToList(),
                TraineeCount = i.CourseSessions
                    .SelectMany(s => s.Enrollments)
                    .Count(e => e.Status != EnrollmentStatus.Dropped)
            })
            .ToListAsync();

        var result = rows.Select(r => new InstructorWorkloadDto
        {
            InstructorId = r.Id,
            InstructorName = r.Name,
            SessionCount = r.SessionCount,
            TotalHours = (int)Math.Round(r.Sessions.Sum(s => (s.EndDateTime - s.StartDateTime).TotalHours)),
            TraineeCount = r.TraineeCount
        }).ToList();

        return Ok(result);
    }

    // GET /api/reports/sessions
    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<SessionReportDto>>> Sessions()
    {
        var data = await db.CourseSessions
            .AsNoTracking()
            .OrderBy(s => s.StartDateTime)
            .Select(s => new SessionReportDto
            {
                SessionId = s.Id,
                CourseTitle = s.Course.Title,
                CategoryName = s.Course.Category.Name,
                InstructorName = s.Instructor.User.FirstName + " " + s.Instructor.User.LastName,
                ClassroomName = s.Classroom.Name,
                StartDateTime = s.StartDateTime,
                Capacity = s.Capacity,
                EnrolledCount = s.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped),
                AttendingCount = s.Enrollments.Count(e => e.Status == EnrollmentStatus.Attending),
                CompletedCount = s.Enrollments.Count(e => e.Status == EnrollmentStatus.Completed),
                DroppedCount = s.Enrollments.Count(e => e.Status == EnrollmentStatus.Dropped),
                SpotsRemaining = s.Capacity - s.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped)
            })
            .ToListAsync();

        return Ok(data);
    }

    // GET /api/reports/certifications
    [HttpGet("certifications")]
    public async Task<ActionResult<IEnumerable<CertificationRateDto>>> Certifications()
    {
        var rows = await db.CertificationTracks
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Name,
                Total = t.TraineeCertifications.Count,
                InProgress = t.TraineeCertifications.Count(tc => tc.Status == CertificationStatus.InProgress),
                Eligible = t.TraineeCertifications.Count(tc => tc.Status == CertificationStatus.Eligible),
                Issued = t.TraineeCertifications.Count(tc => tc.Status == CertificationStatus.Issued)
            })
            .ToListAsync();

        var result = rows.Select(r => new CertificationRateDto
        {
            TrackId = r.Id,
            TrackName = r.Name,
            TotalTrainees = r.Total,
            InProgressCount = r.InProgress,
            EligibleCount = r.Eligible,
            IssuedCount = r.Issued,
            CompletionRate = r.Total == 0 ? 0m : Math.Round(100m * (r.Eligible + r.Issued) / r.Total, 2)
        }).ToList();

        return Ok(result);
    }

    // GET /api/reports/revenue
    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportDto>> Revenue()
    {
        var courseRows = await db.Courses
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Title,
                CategoryName = c.Category.Name,
                Fee = c.EnrollmentFee,
                Enrollments = c.Sessions.SelectMany(s => s.Enrollments)
                    .Where(e => e.Status != EnrollmentStatus.Dropped)
                    .Select(e => new
                    {
                        Collected = e.Payments.Sum(p => (decimal?)p.AmountPaid) ?? 0m
                    }).ToList()
            })
            .ToListAsync();

        var courses = courseRows.Select(c =>
        {
            var totalEnrollments = c.Enrollments.Count;
            var expected = totalEnrollments * c.Fee;
            var collected = c.Enrollments.Sum(e => e.Collected);
            var outstanding = Math.Max(0m, expected - collected);
            return new CourseRevenueDto
            {
                CourseId = c.Id,
                CourseTitle = c.Title,
                CategoryName = c.CategoryName,
                TotalEnrollments = totalEnrollments,
                TotalFeesExpected = expected,
                TotalCollected = collected,
                TotalOutstanding = outstanding,
                CollectionRate = expected == 0 ? 0m : Math.Round(100m * collected / expected, 2)
            };
        }).ToList();

        // Timeline: last 12 months including current.
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => p.PaidAt >= windowStart)
            .Select(p => new { p.PaidAt, p.AmountPaid })
            .ToListAsync();

        var timeline = new List<RevenueTimelinePointDto>();
        for (var i = 0; i < 12; i++)
        {
            var month = windowStart.AddMonths(i);
            var inMonth = payments.Where(p => p.PaidAt.Year == month.Year && p.PaidAt.Month == month.Month).ToList();
            timeline.Add(new RevenueTimelinePointDto
            {
                Month = month.ToString("MMM yyyy"),
                TotalCollected = inMonth.Sum(p => p.AmountPaid),
                PaymentCount = inMonth.Count
            });
        }

        return Ok(new RevenueReportDto { Courses = courses, Timeline = timeline });
    }

    // GET /api/reports/trainees
    [HttpGet("trainees")]
    public async Task<ActionResult<IEnumerable<TraineeReportDto>>> Trainees()
    {
        var data = await db.Trainees
            .AsNoTracking()
            .Select(t => new TraineeReportDto
            {
                TraineeId = t.Id,
                FullName = t.User.FirstName + " " + t.User.LastName,
                TraineePublicId = t.TraineePublicId,
                Email = t.User.Email ?? string.Empty,
                TotalEnrollments = t.Enrollments.Count,
                CompletedCount = t.Enrollments.Count(e => e.Status == EnrollmentStatus.Completed),
                PassedCount = t.Enrollments.Count(e => e.Assessment != null && e.Assessment.Result == AssessmentResult.Pass),
                CertificationsInProgress = t.Certifications.Count(c => c.Status == CertificationStatus.InProgress),
                CertificationsIssued = t.Certifications.Count(c => c.Status == CertificationStatus.Issued)
            })
            .ToListAsync();

        return Ok(data);
    }

    // GET /api/reports/recent-enrollments
    [HttpGet("recent-enrollments")]
    public async Task<ActionResult<IEnumerable<RecentEnrollmentDto>>> RecentEnrollments()
    {
        var data = await db.Enrollments
            .AsNoTracking()
            .OrderByDescending(e => e.EnrolledAt)
            .Take(10)
            .Select(e => new RecentEnrollmentDto
            {
                Id = e.Id,
                TraineeName = e.Trainee.User.FirstName + " " + e.Trainee.User.LastName,
                CourseTitle = e.CourseSession.Course.Title,
                SessionDate = e.CourseSession.StartDateTime,
                Status = e.Status.ToString(),
                EnrolledAt = e.EnrolledAt
            })
            .ToListAsync();

        return Ok(data);
    }

    // GET /api/reports/upcoming-sessions
    [HttpGet("upcoming-sessions")]
    public async Task<ActionResult<IEnumerable<UpcomingSessionDto>>> UpcomingSessions()
    {
        var now = DateTime.UtcNow;
        var data = await db.CourseSessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Scheduled && s.StartDateTime >= now)
            .OrderBy(s => s.StartDateTime)
            .Take(10)
            .Select(s => new UpcomingSessionDto
            {
                Id = s.Id,
                CourseTitle = s.Course.Title,
                InstructorName = s.Instructor.User.FirstName + " " + s.Instructor.User.LastName,
                ClassroomName = s.Classroom.Name,
                StartDateTime = s.StartDateTime,
                Capacity = s.Capacity,
                SpotsRemaining = s.Capacity - s.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped)
            })
            .ToListAsync();

        return Ok(data);
    }
}
