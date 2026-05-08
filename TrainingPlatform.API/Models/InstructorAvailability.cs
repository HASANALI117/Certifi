using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class InstructorAvailability
    {
        public int Id { get; set; }
        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; } = null!;

        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }
}