using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public enum SessionStatus
    {
        Scheduled, InProgress, Completed, Cancelled
    }
    public class CourseSession
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; } = null!;

        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public int Capacity { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

        public ICollection<Enrollment> Enrollments { get; set; } = [];
    }
}