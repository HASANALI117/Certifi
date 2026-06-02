using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.Reports.Services;

namespace TrainingPlatform.Reports.Controllers;

public class ReportsController : ReportControllerBase
{
    private readonly IApiClient _api;

    public ReportsController(IApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Enrollments(CancellationToken ct)
    {
        var data = await _api.GetEnrollmentsAsync(ct);
        return View(data);
    }

    public async Task<IActionResult> Instructors(CancellationToken ct)
    {
        var data = await _api.GetInstructorsAsync(ct);
        return View(data);
    }

    public async Task<IActionResult> Sessions(CancellationToken ct)
    {
        var data = await _api.GetSessionsAsync(ct);
        return View(data);
    }

    public async Task<IActionResult> Certifications(CancellationToken ct)
    {
        var data = await _api.GetCertificationsAsync(ct);
        return View(data);
    }

    public async Task<IActionResult> Revenue(CancellationToken ct)
    {
        var data = await _api.GetRevenueAsync(ct);
        return View(data);
    }

    public async Task<IActionResult> Trainees(CancellationToken ct)
    {
        var data = await _api.GetTraineesAsync(ct);
        return View(data);
    }
}
