using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "TrainingCoordinator")]
public class InstructorsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public InstructorsController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var instructors = await _db.Instructors
            .Include(i => i.User)
            .Include(i => i.CourseSessions)
            .Select(i => new InstructorListItemViewModel
            {
                Id = i.Id,
                FullName = i.User.FirstName + " " + i.User.LastName,
                Email = i.User.Email ?? string.Empty,
                ExpertiseAreas = i.ExpertiseAreas,
                SessionCount = i.CourseSessions.Count
            })
            .ToListAsync();

        return View(instructors);
    }

    public async Task<IActionResult> Details(int id)
    {
        var instructor = await _db.Instructors
            .Include(i => i.User)
            .Include(i => i.CourseSessions)
                .ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null) return NotFound();

        return View(new InstructorDetailsViewModel
        {
            Id = instructor.Id,
            FullName = instructor.User.FirstName + " " + instructor.User.LastName,
            Email = instructor.User.Email ?? string.Empty,
            ExpertiseAreas = instructor.ExpertiseAreas,
            Bio = instructor.Bio,
            UpcomingSessions = instructor.CourseSessions
                .Where(s => s.StartDateTime >= DateTime.Today)
                .Select(s => $"{s.Course.Title} — {s.StartDateTime:d MMM yyyy HH:mm}")
                .ToList()
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new InstructorFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InstructorFormViewModel model)
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

        if (!ModelState.IsValid)
            return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        // Provision the account, assign the Instructor role, and create the profile.
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

        await _userManager.AddToRoleAsync(user, "Instructor");

        _db.Instructors.Add(new Instructor
        {
            UserId = user.Id,
            ExpertiseAreas = model.ExpertiseAreas,
            Bio = model.Bio
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Instructor account created for {model.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var instructor = await _db.Instructors
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (instructor == null) return NotFound();

        return View(new InstructorFormViewModel
        {
            Id            = instructor.Id,
            FirstName     = instructor.User.FirstName,
            LastName      = instructor.User.LastName,
            Email         = instructor.User.Email ?? string.Empty,
            ExpertiseAreas = instructor.ExpertiseAreas,
            Bio           = instructor.Bio
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InstructorFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            ModelState.AddModelError(nameof(model.FirstName), "First name is required.");
        if (string.IsNullOrWhiteSpace(model.LastName))
            ModelState.AddModelError(nameof(model.LastName), "Last name is required.");
        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email is required.");

        if (!ModelState.IsValid)
            return View(model);

        var instructor = await _db.Instructors
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == model.Id);
        if (instructor == null) return NotFound();

        instructor.User.FirstName  = model.FirstName.Trim();
        instructor.User.LastName   = model.LastName.Trim();
        instructor.ExpertiseAreas  = model.ExpertiseAreas;
        instructor.Bio             = model.Bio;

        var newEmail = model.Email.Trim();
        if (!string.Equals(instructor.User.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var token = await _userManager.GenerateChangeEmailTokenAsync(instructor.User, newEmail);
            await _userManager.ChangeEmailAsync(instructor.User, newEmail, token);
            await _userManager.SetUserNameAsync(instructor.User, newEmail);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Instructor profile updated.";
        return RedirectToAction(nameof(Index));
    }
}
