using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace TrainingPlatform.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "TrainingCoordinator")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext db;

        public ReportsController(AppDbContext context)
        {
            db = context;
        }

        [HttpGet("enrollments")]
        public async Task<ActionResult<IEnumerable<EnrollmentStatDto>>> GetEnrollmentStats()
        {
            var stats = await db.Courses
                .Include(c => c.Sessions)
                    .ThenInclude(cs => cs.Enrollments)
                .Select(c => new EnrollmentStatDto
                {
                    CourseName = c.Title,
                    CategoryName = c.CategoryId.ToString(),
                    TotalCapacity = c.Sessions.Sum(cs => cs.Capacity),
                    TotalEnrolled = c.Sessions.SelectMany(cs => cs.Enrollments).Count(),
                    RemainingSpots = c.Sessions.Sum(cs => cs.Capacity) - c.Sessions.SelectMany(cs => cs.Enrollments).Count(),
                    FillPercentage = c.Sessions.Sum(cs => cs.Capacity) == 0 ? 0 :
                        Math.Round((double)c.Sessions.SelectMany(cs => cs.Enrollments).Count() / c.Sessions.Sum(cs => cs.Capacity) * 100, 2)
                })
                .ToListAsync();

            return Ok(stats);
        }
    }
}
