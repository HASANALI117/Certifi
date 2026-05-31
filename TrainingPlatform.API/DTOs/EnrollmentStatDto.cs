namespace TrainingPlatform.API.DTOs
{
    public class EnrollmentStatDto
    {
        public string CourseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int TotalCapacity { get; set; }
        public int TotalEnrolled { get; set; }
        public int RemainingSpots { get; set; }
        public double FillPercentage { get; set; }
    }
}
