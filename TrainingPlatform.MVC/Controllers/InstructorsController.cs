using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Entities;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "Training Coordinator")]
public class InstructorsController : Controller
{
    private readonly TrainingPlatformDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public InstructorsController(TrainingPlatformDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var instructors = await _db.Instructors
            .Include(i => i.User)
            .Include(i => i.Sessions)
            .Select(i => new InstructorListItemViewModel
            {
                Id = i.Id,
                FullName = i.User.FullName,
                Email = i.User.Email ?? string.Empty,
                ExpertiseAreas = i.ExpertiseAreas,
                SessionCount = i.Sessions.Count
            })
            .ToListAsync();

        return View(instructors);
    }

    public async Task<IActionResult> Details(int id)
    {
        var instructor = await _db.Instructors
            .Include(i => i.User)
            .Include(i => i.Sessions)
                .ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instructor == null) return NotFound();

        return View(new InstructorDetailsViewModel
        {
            Id = instructor.Id,
            FullName = instructor.User.FullName,
            Email = instructor.User.Email ?? string.Empty,
            ExpertiseAreas = instructor.ExpertiseAreas,
            Bio = instructor.Bio,
            UpcomingSessions = instructor.Sessions
                .Where(s => s.SessionDate >= DateOnly.FromDateTime(DateTime.Today))
                .Select(s => $"{s.Course.Title} — {s.SessionDate:d MMM yyyy} {s.StartTime:hh\\:mm}")
                .ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(await BuildFormAsync(null));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InstructorFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildFormAsync(model));

        if (await _db.Instructors.AnyAsync(i => i.UserId == model.UserId))
        {
            ModelState.AddModelError("UserId", "This user is already registered as an instructor.");
            return View(await BuildFormAsync(model));
        }

        _db.Instructors.Add(new Instructor
        {
            UserId = model.UserId,
            ExpertiseAreas = model.ExpertiseAreas,
            Bio = model.Bio
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Instructor profile created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var instructor = await _db.Instructors.FindAsync(id);
        if (instructor == null) return NotFound();

        var model = new InstructorFormViewModel
        {
            Id = instructor.Id,
            UserId = instructor.UserId,
            ExpertiseAreas = instructor.ExpertiseAreas,
            Bio = instructor.Bio
        };

        return View(await BuildFormAsync(model));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InstructorFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildFormAsync(model));

        var instructor = await _db.Instructors.FindAsync(model.Id);
        if (instructor == null) return NotFound();

        instructor.ExpertiseAreas = model.ExpertiseAreas;
        instructor.Bio = model.Bio;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Instructor profile updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<InstructorFormViewModel> BuildFormAsync(InstructorFormViewModel? existing)
    {
        var model = existing ?? new InstructorFormViewModel();
        var instructorUsers = await _db.Instructors.Select(i => i.UserId).ToListAsync();

        var instructorRoleUsers = await _userManager.GetUsersInRoleAsync("Instructor");

        model.AvailableUsers = instructorRoleUsers
            .Where(u => !instructorUsers.Contains(u.Id) || u.Id == existing?.UserId)
            .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" });

        return model;
    }
}
