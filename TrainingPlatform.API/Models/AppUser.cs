using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace TrainingPlatform.API.Models
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public Trainee? TraineeProfile { get; set; }
        public Instructor? InstructorProfile { get; set; }
        public ICollection<Notification> Notifications { get; set; } = [];
    }
}