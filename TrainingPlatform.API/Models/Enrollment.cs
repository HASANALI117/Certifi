using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public enum EnrollmentStatus
    {
        Enrolled, Confirmed, Attending, Completed, Dropped
    }
    public class Enrollment
    {
        public int Id { get; set; }
        public int TraineeId { get; set; }
        public Trainee Trainee { get; set; } = null!;

        public int CourseSessionId { get; set; }
        public CourseSession CourseSession { get; set; } = null!;

        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

        public Assessment? Assessment { get; set; }
        public ICollection<Payment> Payments { get; set; } = [];
    }
}