namespace TrainingPlatform.API.DTOs;

public class ReportOverviewDto
{
    public int TotalCourses { get; set; }
    public int TotalSessions { get; set; }
    public int TotalTrainees { get; set; }
    public int TotalInstructors { get; set; }
    public int ActiveEnrollments { get; set; }
    public int CompletedEnrollments { get; set; }
    public int CertificatesIssued { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal OutstandingRevenue { get; set; }
}

public class EnrollmentStatReportDto
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalEnrollments { get; set; }
    public int EnrolledCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int AttendingCount { get; set; }
    public int CompletedCount { get; set; }
    public int DroppedCount { get; set; }
}

public class InstructorWorkloadDto
{
    public int InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int TotalHours { get; set; }
    public int TraineeCount { get; set; }
}

public class SessionReportDto
{
    public int SessionId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int AttendingCount { get; set; }
    public int CompletedCount { get; set; }
    public int DroppedCount { get; set; }
    public int SpotsRemaining { get; set; }
}

public class CertificationRateDto
{
    public int TrackId { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public int TotalTrainees { get; set; }
    public int InProgressCount { get; set; }
    public int EligibleCount { get; set; }
    public int IssuedCount { get; set; }
    public decimal CompletionRate { get; set; }
}

public class CourseRevenueDto
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalEnrollments { get; set; }
    public decimal TotalFeesExpected { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal CollectionRate { get; set; }
}

public class RevenueTimelinePointDto
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public int PaymentCount { get; set; }
}

public class RevenueReportDto
{
    public List<CourseRevenueDto> Courses { get; set; } = new();
    public List<RevenueTimelinePointDto> Timeline { get; set; } = new();
}

public class TraineeReportDto
{
    public int TraineeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string TraineePublicId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalEnrollments { get; set; }
    public int CompletedCount { get; set; }
    public int PassedCount { get; set; }
    public int CertificationsInProgress { get; set; }
    public int CertificationsIssued { get; set; }
}

public class RecentEnrollmentDto
{
    public int Id { get; set; }
    public string TraineeName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

public class UpcomingSessionDto
{
    public int Id { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public int Capacity { get; set; }
    public int SpotsRemaining { get; set; }
}
