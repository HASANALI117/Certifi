using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.DTOs;

namespace TrainingPlatform.API.Controllers;

[ApiController]
[Route("api/certifications")]
public class CertificationsController(AppDbContext db) : ControllerBase
{
    // GET /api/certifications/verify?traineeId=TR-0001&certRef=CERT-2026-001
    // Public — used by the unauthenticated "Verify a certificate" page.
    [HttpGet("verify")]
    public async Task<ActionResult<CertificationVerifyResponse>> Verify(
        [FromQuery] string traineeId,
        [FromQuery] string certRef)
    {
        if (string.IsNullOrWhiteSpace(traineeId) || string.IsNullOrWhiteSpace(certRef))
            return BadRequest("traineeId and certRef are required.");

        var cert = await db.TraineeCertifications
            .AsNoTracking()
            .Include(tc => tc.Trainee).ThenInclude(t => t.User)
            .Include(tc => tc.CertificationTrack)
                .ThenInclude(ct => ct.CertificationTrackCourses)
            .FirstOrDefaultAsync(tc =>
                tc.Trainee.TraineePublicId == traineeId
                && tc.CertRefNumber == certRef);

        if (cert is null) return NotFound();

        var trackCourseIds = cert.CertificationTrack.CertificationTrackCourses
            .Select(ctc => ctc.CourseId)
            .ToList();

        var completedCourses = await db.Enrollments
            .AsNoTracking()
            .Where(e =>
                e.TraineeId == cert.TraineeId
                && trackCourseIds.Contains(e.CourseSession.CourseId)
                && e.Assessment != null)
            .Select(e => new CompletedCourseDto
            {
                Title = e.CourseSession.Course.Title,
                CompletedDate = e.Assessment!.RecordedAt,
                Result = e.Assessment!.Result.ToString()
            })
            .ToListAsync();

        return Ok(new CertificationVerifyResponse
        {
            TraineeName = $"{cert.Trainee.User.FirstName} {cert.Trainee.User.LastName}".Trim(),
            CertificationTrack = cert.CertificationTrack.Name,
            CertRefNumber = cert.CertRefNumber,
            Status = cert.Status.ToString(),
            IssuedDate = cert.IssuedAt,
            CompletedCourses = completedCourses
        });
    }
}
