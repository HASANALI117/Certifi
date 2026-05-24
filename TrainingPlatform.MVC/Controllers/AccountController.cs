using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly AppDbContext _db;

    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
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

        await _signInManager.SignInAsync(user, isPersistent: false);
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

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
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
