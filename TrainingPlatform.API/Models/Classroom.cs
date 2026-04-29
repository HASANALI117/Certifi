using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class Classroom
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }

        public ICollection<ClassroomEquipment> Equipment { get; set; } = [];
        public ICollection<CourseSession> CourseSessions { get; set; } = [];
    }
}