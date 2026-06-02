namespace TrainingPlatform.Reports.Models;

public class OverviewViewModel
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

public class EnrollmentReportRow
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

public class InstructorReportRow
{
    public int InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public decimal TotalHours { get; set; }
    public int TraineeCount { get; set; }
}

public class SessionReportRow
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

public class CertificationReportRow
{
    public int TrackId { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public int TotalTrainees { get; set; }
    public int InProgressCount { get; set; }
    public int EligibleCount { get; set; }
    public int IssuedCount { get; set; }
    public decimal CompletionRate { get; set; }
}

public class RevenueReportViewModel
{
    public List<RevenueCourseRow> Courses { get; set; } = new();
    public List<RevenueTimelinePoint> Timeline { get; set; } = new();
}

public class RevenueCourseRow
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

public class RevenueTimelinePoint
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public int PaymentCount { get; set; }
}

public class TraineeReportRow
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

public class RecentEnrollmentRow
{
    public int Id { get; set; }
    public string TraineeName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public DateTime? SessionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

public class UpcomingSessionRow
{
    public int Id { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public int Capacity { get; set; }
    public int SpotsRemaining { get; set; }
}

public class DashboardViewModel
{
    public OverviewViewModel Overview { get; set; } = new();
    public List<RecentEnrollmentRow> RecentEnrollments { get; set; } = new();
    public List<UpcomingSessionRow> UpcomingSessions { get; set; } = new();
}
