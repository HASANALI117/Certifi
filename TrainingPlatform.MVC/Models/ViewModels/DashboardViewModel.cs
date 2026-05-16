using TrainingPlatform.API.Models;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class DashboardViewModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<DashboardCategoryTab> Categories { get; set; } = [];
    public int? SelectedCategoryId { get; set; }
    public string? Search { get; set; }

    public IReadOnlyList<DashboardCourseCard> Courses { get; set; } = [];
    public IReadOnlyList<DashboardSession> UpcomingSessions { get; set; } = [];
    public IReadOnlyList<DashboardProgressItem> LearningProgress { get; set; } = [];

    public DashboardStats Stats { get; set; } = new();
}

public class DashboardCategoryTab
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CourseCount { get; set; }
}

public class DashboardCourseCard
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public int DurationHours { get; set; }
    public int Capacity { get; set; }
    public DateTime? NextSessionStart { get; set; }
    public DateTime? NextSessionEnd { get; set; }
    public string AccentSeed { get; set; } = string.Empty;
}

public class DashboardSession
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public SessionStatus Status { get; set; }
}

public class DashboardProgressItem
{
    public string Label { get; set; } = string.Empty;
    public string Sublabel { get; set; } = string.Empty;
    public int Percent { get; set; }
    public string Accent { get; set; } = "primary";
}

public class DashboardStats
{
    public int CourseCount { get; set; }
    public int UpcomingSessionCount { get; set; }
    public int ActiveEnrollmentCount { get; set; }
    public int CertificationCount { get; set; }
}
