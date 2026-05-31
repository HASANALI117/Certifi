using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.Reports.Services;

namespace TrainingPlatform.Reports.Controllers
{
    [Authorize(Roles = "TrainingCoordinator")]
    public class ReportController : Controller
    {
        private readonly ReportService _reportService;

        public ReportController(ReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> Enrollments()
        {
            var data = await _reportService.GetEnrollmentsAsync();
            return View(data);
        }
    }
}
