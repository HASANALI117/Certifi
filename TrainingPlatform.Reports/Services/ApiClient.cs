using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TrainingPlatform.Reports.Auth;
using TrainingPlatform.Reports.Models;

namespace TrainingPlatform.Reports.Services;

public class ApiClient : IApiClient
{
    public const string AuthTokenClaimType = SharedCookie.TokenClaimType;

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClient(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ApiClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Task<OverviewViewModel> GetOverviewAsync(CancellationToken ct = default)
        => GetAsync<OverviewViewModel>("/api/reports/overview", ct)!;

    public Task<List<EnrollmentReportRow>> GetEnrollmentsAsync(CancellationToken ct = default)
        => GetListAsync<EnrollmentReportRow>("/api/reports/enrollments", ct);

    public Task<List<InstructorReportRow>> GetInstructorsAsync(CancellationToken ct = default)
        => GetListAsync<InstructorReportRow>("/api/reports/instructors", ct);

    public Task<List<SessionReportRow>> GetSessionsAsync(CancellationToken ct = default)
        => GetListAsync<SessionReportRow>("/api/reports/sessions", ct);

    public Task<List<CertificationReportRow>> GetCertificationsAsync(CancellationToken ct = default)
        => GetListAsync<CertificationReportRow>("/api/reports/certifications", ct);

    public Task<RevenueReportViewModel> GetRevenueAsync(CancellationToken ct = default)
        => GetAsync<RevenueReportViewModel>("/api/reports/revenue", ct)!;

    public Task<List<TraineeReportRow>> GetTraineesAsync(CancellationToken ct = default)
        => GetListAsync<TraineeReportRow>("/api/reports/trainees", ct);

    public Task<List<RecentEnrollmentRow>> GetRecentEnrollmentsAsync(CancellationToken ct = default)
        => GetListAsync<RecentEnrollmentRow>("/api/reports/recent-enrollments", ct);

    public Task<List<UpcomingSessionRow>> GetUpcomingSessionsAsync(CancellationToken ct = default)
        => GetListAsync<UpcomingSessionRow>("/api/reports/upcoming-sessions", ct);

    private async Task<List<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        var result = await GetAsync<List<T>>(path, ct);
        return result ?? new List<T>();
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        var token = GetCurrentToken();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Request to API ({Path}) failed.", path);
            throw new ApiUnavailableException("The reporting service could not reach the API.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Request to API ({Path}) timed out.", path);
            throw new ApiUnavailableException("The reporting service timed out contacting the API.", ex);
        }

        try
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new ApiUnauthorizedException("The API rejected the bearer token.");
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        finally
        {
            response.Dispose();
        }
    }

    private string? GetCurrentToken()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(AuthTokenClaimType)?.Value;
    }
}
