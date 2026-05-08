using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class Trainee
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;

        public string TraineePublicId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; } = [];
        public ICollection<TraineeCertification> Certifications { get; set; } = [];
    }
}