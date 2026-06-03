using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

public class CoursesController : Controller
{
    private const string UploadFolder = "images/courses";
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CoursesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private static string EscapeLike(string input) =>
        input.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = _db.Courses
            .Include(c => c.Category)
            .Include(c => c.PrerequisiteCourse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // SQL LIKE under default CI collation gives case-insensitive matching.
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Title, pattern) ||
                EF.Functions.Like(c.Description, pattern));
        }

        if (categoryId.HasValue)
            query = query.Where(c => c.CategoryId == categoryId.Value);

        var courses = await query
            .Select(c => new CourseListItemViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CategoryName = c.Category.Name,
                DurationHours = c.DurationHours,
                Capacity = c.Capacity,
                Fee = c.EnrollmentFee,
                PrerequisiteTitle = c.PrerequisiteCourse != null ? c.PrerequisiteCourse.Title : null,
                ImageUrl = c.ImageUrl,
                SessionCount = c.Sessions.Count
            })
            .ToListAsync();

        var categoryStats = await _db.CourseCategories
            .Select(c => new CategoryWidgetViewModel
            {
                Id = c.Id,
                Name = c.Name,
                CourseCount = _db.Courses.Count(co => co.CategoryId == c.Id)
            })
            .ToListAsync();

        return View(new CourseCatalogViewModel
        {
            Courses = courses,
            CategoryStats = categoryStats,
            Search = search,
            SelectedCategoryId = categoryId
        });
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(await BuildFormViewModelAsync(null));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseFormViewModel model)
    {
        ValidateImageFile(model);

        if (!ModelState.IsValid)
            return View(await BuildFormViewModelAsync(model));

        string? imageUrl = await ResolveImageUrlAsync(model, currentImageUrl: null);

        _db.Courses.Add(new Course
        {
            Title = model.Title,
            Description = model.Description,
            DurationHours = model.DurationHours,
            Capacity = model.Capacity,
            EnrollmentFee = model.Fee,
            CategoryId = model.CategoryId,
            PrerequisiteCourseId = model.PrerequisiteCourseId,
            ImageUrl = imageUrl
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Course '{model.Title}' created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "TrainingCoordinator")]
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
            Fee = course.EnrollmentFee,
            CategoryId = course.CategoryId,
            PrerequisiteCourseId = course.PrerequisiteCourseId,
            ImageUrl = course.ImageUrl,
            ExistingImageUrl = course.ImageUrl
        };

        return View(await BuildFormViewModelAsync(model));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseFormViewModel model)
    {
        ValidateImageFile(model);

        var course = await _db.Courses.FindAsync(model.Id);
        if (course == null) return NotFound();

        model.ExistingImageUrl = course.ImageUrl;

        if (!ModelState.IsValid)
            return View(await BuildFormViewModelAsync(model));

        string? newImageUrl = await ResolveImageUrlAsync(model, currentImageUrl: course.ImageUrl);

        // Delete previously stored upload if we are replacing it or removing it.
        if (!string.Equals(course.ImageUrl, newImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            DeleteUploadedImageIfOwned(course.ImageUrl);
        }

        course.Title = model.Title;
        course.Description = model.Description;
        course.DurationHours = model.DurationHours;
        course.Capacity = model.Capacity;
        course.EnrollmentFee = model.Fee;
        course.CategoryId = model.CategoryId;
        course.PrerequisiteCourseId = model.PrerequisiteCourseId;
        course.ImageUrl = newImageUrl;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Course updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();

        DeleteUploadedImageIfOwned(course.ImageUrl);
        course.ImageUrl = null;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Course image removed.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // Defensive GET fallback — bounce direct hits back to the Edit page.
    [Authorize(Roles = "TrainingCoordinator")]
    [HttpGet]
    public IActionResult DeleteImage() => RedirectToAction(nameof(Index));

    [Authorize(Roles = "TrainingCoordinator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();

        DeleteUploadedImageIfOwned(course.ImageUrl);
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course deleted.";
        return RedirectToAction(nameof(Index));
    }

    // No GET Delete page — bounce direct hits (refresh, back button, AJAX fallback) to the list.
    [HttpGet]
    public IActionResult Delete() => RedirectToAction(nameof(Index));

    private async Task<CourseFormViewModel> BuildFormViewModelAsync(CourseFormViewModel? existing)
    {
        var model = existing ?? new CourseFormViewModel();

        model.Categories = await _db.CourseCategories
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();

        model.Courses = await _db.Courses
            .Where(c => existing == null || c.Id != existing.Id)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
            .ToListAsync();

        return model;
    }

    private void ValidateImageFile(CourseFormViewModel model)
    {
        if (model.ImageFile == null || model.ImageFile.Length == 0) return;

        if (model.ImageFile.Length > MaxImageBytes)
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Image must be 5 MB or smaller.");
            return;
        }

        var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(model.ImageFile),
                $"Unsupported image type. Allowed: {string.Join(", ", AllowedImageExtensions)}.");
        }
    }

    private async Task<string?> ResolveImageUrlAsync(CourseFormViewModel model, string? currentImageUrl)
    {
        // 1) New file upload wins.
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            return await SaveUploadedImageAsync(model.ImageFile);
        }

        // 2) Explicit URL provided (and different from existing) replaces.
        if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            return model.ImageUrl.Trim();
        }

        // 3) Otherwise keep what the course already has — image removal goes through DeleteImage.
        return currentImageUrl;
    }

    private async Task<string> SaveUploadedImageAsync(IFormFile file)
    {
        var webRoot = _env.WebRootPath;
        var folder = Path.Combine(webRoot, UploadFolder);
        Directory.CreateDirectory(folder);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return $"/{UploadFolder}/{fileName}";
    }

    private void DeleteUploadedImageIfOwned(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        // Only delete images we uploaded ourselves, not links or the built-in images.
        var prefix = $"/{UploadFolder}/";
        if (!imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

        var relative = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_env.WebRootPath, relative);

        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); }
            catch { /* best-effort cleanup */ }
        }
    }
}
