using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.Reports.Models;
using TrainingPlatform.Reports.Services;

namespace TrainingPlatform.Reports.Controllers;

public class DashboardController : ReportControllerBase
{
    private readonly IApiClient _api;

    public DashboardController(IApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var overviewTask = _api.GetOverviewAsync(ct);
        var recentTask = _api.GetRecentEnrollmentsAsync(ct);
        var upcomingTask = _api.GetUpcomingSessionsAsync(ct);

        await Task.WhenAll(overviewTask, recentTask, upcomingTask);

        var model = new DashboardViewModel
        {
            Overview = overviewTask.Result,
            RecentEnrollments = recentTask.Result,
            UpcomingSessions = upcomingTask.Result
        };

        return View(model);
    }
}
