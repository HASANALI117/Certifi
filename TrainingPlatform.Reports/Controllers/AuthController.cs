using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.Reports.Models;
using TrainingPlatform.Reports.Services;

namespace TrainingPlatform.Reports.Controllers;

[AllowAnonymous]
public class AuthController : Controller
{
    private const string CoordinatorRole = "TrainingCoordinator";

    private readonly IApiClient _api;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IApiClient api, ILogger<AuthController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        LoginResponse? response;
        try
        {
            response = await _api.LoginAsync(model.Email, model.Password);
        }
        catch (ApiUnavailableException ex)
        {
            _logger.LogWarning(ex, "API unavailable during login.");
            ModelState.AddModelError(string.Empty,
                "The reporting service is currently unavailable. Please try again shortly.");
            return View(model);
        }

        if (response is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (!string.Equals(response.Role, CoordinatorRole, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty,
                "Reporting is restricted to Training Coordinators.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, response.Email),
            new(ClaimTypes.Name, response.FullName),
            new(ClaimTypes.Email, response.Email),
            new(ClaimTypes.Role, response.Role),
            new(ApiClient.AuthTokenClaimType, response.Token)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
