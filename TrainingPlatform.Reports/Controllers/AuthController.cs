using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.Reports.Auth;
using TrainingPlatform.Reports.Services;

namespace TrainingPlatform.Reports.Controllers;

[AllowAnonymous]
public class AuthController(INavLinks navLinks) : Controller
{
    private readonly INavLinks _navLinks = navLinks;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Clears the shared cookie, signing the user out of both apps, then hands off to MVC login.
        await HttpContext.SignOutAsync(SharedCookie.Scheme);
        return Redirect(_navLinks.Mvc("/Account/Login"));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
