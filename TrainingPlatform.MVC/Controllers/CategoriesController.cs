using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Entities;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "Training Coordinator")]
public class CategoriesController : Controller
{
    private readonly TrainingPlatformDbContext _db;

    public CategoriesController(TrainingPlatformDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var categories = await _db.Categories
            .Include(c => c.Courses)
            .Select(c => new CategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                CourseCount = c.Courses.Count
            })
            .ToListAsync();

        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => View(new CategoryViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _db.Categories.Add(new Category { Name = model.Name });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Category '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound();
        return View(new CategoryViewModel { Id = category.Id, Name = category.Name });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var category = await _db.Categories.FindAsync(model.Id);
        if (category == null) return NotFound();

        category.Name = model.Name;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Courses).FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        if (category.Courses.Any())
        {
            TempData["Error"] = "Cannot delete a category that has courses assigned to it.";
            return RedirectToAction(nameof(Index));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(Index));
    }
}
