using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;

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
            var session = await _db.CourseSessions
                .Include(cs => cs.Enrollments)
                .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

            if (session == null) return;

            int enrolledCount = session.Enrollments.Count;
            int remainingSpots = session.Capacity - enrolledCount;

            var data = new
            {
                CourseSessionId = courseSessionId,
                EnrolledCount = enrolledCount,
                Capacity = session.Capacity,
                RemainingSpots = remainingSpots,
                IsFull = remainingSpots <= 0
            };

            await Clients.All.SendAsync("EnrollmentUpdated", data);
        }

        public async Task GetEnrollmentCount(int courseSessionId)
        {
            var session = await _db.CourseSessions
                .Include(cs => cs.Enrollments)
                .FirstOrDefaultAsync(cs => cs.Id == courseSessionId);

            if (session == null) return;

            int enrolledCount = session.Enrollments.Count;
            int remainingSpots = session.Capacity - enrolledCount;

            var data = new
            {
                CourseSessionId = courseSessionId,
                EnrolledCount = enrolledCount,
                Capacity = session.Capacity,
                RemainingSpots = remainingSpots,
                IsFull = remainingSpots <= 0
            };

            await Clients.Caller.SendAsync("EnrollmentUpdated", data);
        }
    }
}