using System;

namespace TrainingPlatform.MVC.Models.ViewModels
{
    public class AvailableSessionViewModel
    {
        public int Id { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string ClassroomName { get; set; } = string.Empty;
        public DateOnly SessionDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public decimal Fee { get; set; }
        public int Capacity { get; set; }
        public int EnrolledCount { get; set; }
        public int AvailableSpots { get; set; }
    }
}
