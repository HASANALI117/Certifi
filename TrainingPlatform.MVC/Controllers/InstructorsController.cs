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
            .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FirstName} {u.LastName} ({u.Email})" });

        return model;
    }
}
