using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.Hubs
{
    public class EnrollmentHub : Hub
    {
        private readonly AppDbContext _db;

        public EnrollmentHub(AppDbContext db)
        {
            _db = db;
        }

        public async Task UpdateEnrollmentCount(int courseSessionId)
        {
            var data = await BuildEnrollmentCounterAsync(courseSessionId);
            if (data != null)
                await Clients.All.SendAsync("EnrollmentUpdated", data);
        }

        public async Task GetEnrollmentCount(int courseSessionId)
        {
            var data = await BuildEnrollmentCounterAsync(courseSessionId);
            if (data != null)
                await Clients.Caller.SendAsync("EnrollmentUpdated", data);
        }

        private async Task<object?> BuildEnrollmentCounterAsync(int courseSessionId)
        {
            var session = await _db.CourseSessions
                .Include(cs => cs.Enrollments)
                .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

            if (session == null) return null;

            var enrolledCount = session.Enrollments.Count(e => e.Status != EnrollmentStatus.Dropped);
            var remainingSpots = Math.Max(0, session.Capacity - enrolledCount);

            return new
            {
                CourseSessionId = courseSessionId,
                EnrolledCount = enrolledCount,
                Capacity = session.Capacity,
                RemainingSpots = remainingSpots,
                IsFull = remainingSpots <= 0
            };
        }
    }
}
