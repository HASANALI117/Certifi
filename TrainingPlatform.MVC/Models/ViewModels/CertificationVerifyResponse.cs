namespace TrainingPlatform.MVC.Models.ViewModels;

public class CertificationVerifyResponse
{
    public string TraineeName { get; set; } = string.Empty;
    public string CertificationTrack { get; set; } = string.Empty;
    public string CertRefNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }
    public List<CompletedCourseDto> CompletedCourses { get; set; } = new();
}

public class CompletedCourseDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public string Result { get; set; } = string.Empty;
}
