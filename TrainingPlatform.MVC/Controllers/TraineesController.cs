using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "TrainingCoordinator")]
public class TraineesController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public TraineesController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var trainees = await _db.Trainees
            .Include(t => t.User)
            .Include(t => t.Enrollments)
            .Select(t => new TraineeListItemViewModel
            {
                Id = t.Id,
                TraineePublicId = t.TraineePublicId,
                FullName = t.User.FirstName + " " + t.User.LastName,
                Email = t.User.Email ?? string.Empty,
                Phone = t.Phone,
                EnrollmentCount = t.Enrollments.Count
            })
            .ToListAsync();

        return View(trainees);
    }

    public async Task<IActionResult> Details(int id)
    {
        var trainee = await _db.Trainees
            .Include(t => t.User)
            .Include(t => t.Enrollments)
                .ThenInclude(e => e.CourseSession)
                    .ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trainee == null) return NotFound();

        return View(new TraineeDetailsViewModel
        {
            Id = trainee.Id,
            TraineePublicId = trainee.TraineePublicId,
            FullName = trainee.User.FirstName + " " + trainee.User.LastName,
            Email = trainee.User.Email ?? string.Empty,
            Phone = trainee.Phone,
            DateOfBirth = trainee.DateOfBirth,
            UpcomingEnrollments = trainee.Enrollments
                .Where(e => e.CourseSession.StartDateTime >= DateTime.Today)
                .Select(e => $"{e.CourseSession.Course.Title} — {e.CourseSession.StartDateTime:d MMM yyyy HH:mm}")
                .ToList()
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new TraineeFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TraineeFormViewModel model)
    {
        // Account fields are required on create (manual — VM is shared with Edit).
        if (string.IsNullOrWhiteSpace(model.FirstName))
            ModelState.AddModelError(nameof(model.FirstName), "First name is required.");
        if (string.IsNullOrWhiteSpace(model.LastName))
            ModelState.AddModelError(nameof(model.LastName), "Last name is required.");
        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(model.TempPassword) || model.TempPassword.Length < 8)
            ModelState.AddModelError(nameof(model.TempPassword), "Temporary password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Phone is required.");
        if (model.DateOfBirth == default)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth is required.");

        if (!ModelState.IsValid)
            return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        // Provision the account, assign the Trainee role, and create the profile.
        var user = new AppUser
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.TempPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "Trainee");

        _db.Trainees.Add(new Trainee
        {
            UserId = user.Id,
            TraineePublicId = await GenerateTraineePublicIdAsync(),
            Phone = model.Phone,
            DateOfBirth = model.DateOfBirth
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Trainee account created for {model.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var trainee = await _db.Trainees
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trainee == null) return NotFound();

        return View(new TraineeFormViewModel
        {
            Id          = trainee.Id,
            FirstName   = trainee.User.FirstName,
            LastName    = trainee.User.LastName,
            Email       = trainee.User.Email ?? string.Empty,
            Phone       = trainee.Phone,
            DateOfBirth = trainee.DateOfBirth
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TraineeFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            ModelState.AddModelError(nameof(model.FirstName), "First name is required.");
        if (string.IsNullOrWhiteSpace(model.LastName))
            ModelState.AddModelError(nameof(model.LastName), "Last name is required.");
        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Phone is required.");
        if (model.DateOfBirth == default)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth is required.");

        if (!ModelState.IsValid)
            return View(model);

        var trainee = await _db.Trainees
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == model.Id);
        if (trainee == null) return NotFound();

        trainee.User.FirstName = model.FirstName.Trim();
        trainee.User.LastName  = model.LastName.Trim();
        trainee.Phone          = model.Phone;
        trainee.DateOfBirth    = model.DateOfBirth;

        var newEmail = model.Email.Trim();
        if (!string.Equals(trainee.User.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var token = await _userManager.GenerateChangeEmailTokenAsync(trainee.User, newEmail);
            await _userManager.ChangeEmailAsync(trainee.User, newEmail, token);
            await _userManager.SetUserNameAsync(trainee.User, newEmail);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Trainee profile updated.";
        return RedirectToAction(nameof(Index));
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
        return $"{year}{Guid.NewGuid().ToString("N")[..5]}";
    }
}
