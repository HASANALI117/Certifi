using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Entities;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

public class CoursesController : Controller
{
    private readonly TrainingPlatformDbContext _db;

    public CoursesController(TrainingPlatformDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = _db.Courses
            .Include(c => c.Category)
            .Include(c => c.PrerequisiteCourse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(c => c.CategoryId == categoryId.Value);

        var courses = await query
            .Select(c => new CourseListItemViewModel
            {
                Id = c.Id,
                Title = c.Title,
                CategoryName = c.Category.Name,
                DurationHours = c.DurationHours,
                Capacity = c.Capacity,
                Fee = c.Fee,
                PrerequisiteTitle = c.PrerequisiteCourse != null ? c.PrerequisiteCourse.Title : null
            })
            .ToListAsync();

        ViewBag.Categories = await _db.Categories
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.Search = search;

        return View(courses);
    }

    public async Task<IActionResult> Details(int id)
    {
        var course = await _db.Courses
            .Include(c => c.Category)
            .Include(c => c.PrerequisiteCourse)
            .Include(c => c.Sessions)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null) return NotFound();

        return View(new CourseDetailsViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            CategoryName = course.Category.Name,
            DurationHours = course.DurationHours,
            Capacity = course.Capacity,
            Fee = course.Fee,
            PrerequisiteTitle = course.PrerequisiteCourse?.Title,
            SessionCount = course.Sessions.Count
        });
    }

    [Authorize(Roles = "Training Coordinator")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(await BuildFormViewModelAsync(null));
    }

    [Authorize(Roles = "Training Coordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildFormViewModelAsync(model));

        _db.Courses.Add(new Course
        {
            Title = model.Title,
            Description = model.Description,
            DurationHours = model.DurationHours,
            Capacity = model.Capacity,
            Fee = model.Fee,
            CategoryId = model.CategoryId,
            PrerequisiteCourseId = model.PrerequisiteCourseId
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Course '{model.Title}' created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Training Coordinator")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();

        var model = new CourseFormViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            DurationHours = course.DurationHours,
            Capacity = course.Capacity,
            Fee = course.Fee,
            CategoryId = course.CategoryId,
            PrerequisiteCourseId = course.PrerequisiteCourseId
        };

        return View(await BuildFormViewModelAsync(model));
    }

    [Authorize(Roles = "Training Coordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildFormViewModelAsync(model));

        var course = await _db.Courses.FindAsync(model.Id);
        if (course == null) return NotFound();

        course.Title = model.Title;
        course.Description = model.Description;
        course.DurationHours = model.DurationHours;
        course.Capacity = model.Capacity;
        course.Fee = model.Fee;
        course.CategoryId = model.CategoryId;
        course.PrerequisiteCourseId = model.PrerequisiteCourseId;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Course updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Training Coordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CourseFormViewModel> BuildFormViewModelAsync(CourseFormViewModel? existing)
    {
        var model = existing ?? new CourseFormViewModel();

        model.Categories = await _db.Categories
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();

        model.Courses = await _db.Courses
            .Where(c => existing == null || c.Id != existing.Id)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
            .ToListAsync();

        return model;
    }
}
