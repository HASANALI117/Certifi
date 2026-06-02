using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Infrastructure;
using TrainingPlatform.MVC.Models.ViewModels;
using TrainingPlatform.MVC.Services;

namespace TrainingPlatform.MVC.Controllers;

public class AccountController : Controller
{
    // Claim key the Reports app reads to call the API as the signed-in user.
    // Must match TrainingPlatform.Reports.Auth.SharedCookie.TokenClaimType.
    private const string ApiAccessTokenClaim = "ApiAccessToken";

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IUserClaimsPrincipalFactory<AppUser> _claimsFactory;
    private readonly IAuthApiClient _authApi;
    private readonly AppDbContext _db;
    private readonly INavLinks _navLinks;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IUserClaimsPrincipalFactory<AppUser> claimsFactory,
        IAuthApiClient authApi,
        AppDbContext db,
        INavLinks navLinks)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _claimsFactory = claimsFactory;
        _authApi = authApi;
        _db = db;
        _navLinks = navLinks;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Self-registration is Trainee-only. Instructors/coordinators are provisioned
        // by the coordinator.
        var user = new AppUser
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "Trainee");

        // Create the Trainee profile so the account can enroll, pay, and earn certs.
        _db.Trainees.Add(new Trainee
        {
            UserId = user.Id,
            TraineePublicId = await GenerateTraineePublicIdAsync(),
            Phone = model.Phone,
            DateOfBirth = model.DateOfBirth
        });
        await _db.SaveChangesAsync();

        await IssueCookieWithApiTokenAsync(user, model.Email, model.Password, isPersistent: false);
        return RedirectToAction("Index", "Dashboard");
    }

    // TraineePublicId format: {year}{random 5 digits}, e.g. 202603655. Retry on collision.
    private async Task<string> GenerateTraineePublicIdAsync()
    {
        var year = DateTime.UtcNow.Year;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"{year}{Random.Shared.Next(0, 100000):D5}";
            if (!await _db.Trainees.AnyAsync(t => t.TraineePublicId == candidate))
                return candidate;
        }
        // Extremely unlikely fallback — widen entropy.
        return $"{year}{Guid.NewGuid().ToString("N")[..5]}";
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var pwCheck = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
        if (!pwCheck.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        await IssueCookieWithApiTokenAsync(user, model.Email, model.Password, model.RememberMe);

        // A local return URL (e.g. a deep-link that bounced through login) wins.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        // Coordinators land on the Reports overview; other roles on the dashboard.
        if (await _userManager.IsInRoleAsync(user, "TrainingCoordinator"))
            return Redirect(_navLinks.Reports("/Dashboard"));

        return RedirectToAction("Index", "Dashboard");
    }

    // One sign-in flow: build the Identity principal, attach the API JWT as a
    // claim, then issue the shared auth cookie. The Reports app reads that
    // claim to call the API on behalf of the same user without a second login.
    private async Task IssueCookieWithApiTokenAsync(AppUser user, string email, string password, bool isPersistent)
    {
        var jwt = await _authApi.LoginAsync(email, password);

        var principal = await _claimsFactory.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        if (!string.IsNullOrEmpty(jwt))
        {
            identity.AddClaim(new Claim(ApiAccessTokenClaim, jwt));
        }
        else
        {
            // Cookie still issued so MVC features keep working; Reports app will
            // bounce the user to its own login if they try to open it.
            TempData["DashWarning"] = "Reporting features are temporarily unavailable.";
        }

        var props = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, props);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
