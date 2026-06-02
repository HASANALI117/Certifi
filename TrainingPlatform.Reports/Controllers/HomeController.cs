using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.Reports.Models;

namespace TrainingPlatform.Reports.Controllers;

public class HomeController : Controller
{
    [Authorize(Policy = "TrainingCoordinator")]
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [AllowAnonymous]
    public IActionResult ServiceUnavailable() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
