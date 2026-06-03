using TrainingPlatform.Reports.Models;

namespace TrainingPlatform.Reports.Services;

public interface IApiClient
{
    Task<OverviewViewModel> GetOverviewAsync(CancellationToken ct = default);
    Task<List<EnrollmentReportRow>> GetEnrollmentsAsync(CancellationToken ct = default);
    Task<List<InstructorReportRow>> GetInstructorsAsync(CancellationToken ct = default);
    Task<List<SessionReportRow>> GetSessionsAsync(CancellationToken ct = default);
    Task<List<CertificationReportRow>> GetCertificationsAsync(CancellationToken ct = default);
    Task<RevenueReportViewModel> GetRevenueAsync(CancellationToken ct = default);
    Task<List<TraineeReportRow>> GetTraineesAsync(CancellationToken ct = default);
    Task<List<RecentEnrollmentRow>> GetRecentEnrollmentsAsync(CancellationToken ct = default);
    Task<List<UpcomingSessionRow>> GetUpcomingSessionsAsync(CancellationToken ct = default);
}
