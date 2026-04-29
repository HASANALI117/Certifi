using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class Instructor
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public string Bio { get; set; } = string.Empty;

        public ICollection<InstructorAvailability> Availability { get; set; } = [];
        public ICollection<CourseSession> CourseSessions { get; set; } = [];
    }
}