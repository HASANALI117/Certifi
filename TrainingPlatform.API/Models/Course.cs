using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class Course
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public CourseCategory Category { get; set; } = null!;

        public int? PrerequisiteCourseId { get; set; }
        public Course? PrerequisiteCourse { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationHours { get; set; }
        public int Capacity { get; set; }
        public decimal EnrollmentFee { get; set; }

        public ICollection<CourseSession> Sessions { get; set; } = [];
        public ICollection<CertificationTrackCourse> CertificationTrackCourses { get; set; } = [];
    }
}