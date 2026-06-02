using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

// Coordinator-only management of certification track templates
// (CertificationTrack) and their required courses (CertificationTrackCourse).
[Authorize(Roles = "TrainingCoordinator")]
public class CertificationTracksController : Controller
{
    private readonly AppDbContext _db;

    public CertificationTracksController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var tracks = await _db.CertificationTracks
            .Include(t => t.CertificationTrackCourses)
            .Include(t => t.TraineeCertifications)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(tracks);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCoursesAsync();
        return View(new CertificationTrackFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CertificationTrackFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync();
            return View(model);
        }

        var track = new CertificationTrack
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim() ?? string.Empty,
            CertRefPrefix = model.CertRefPrefix?.Trim() ?? string.Empty
        };

        foreach (var courseId in model.SelectedCourseIds.Distinct())
            track.CertificationTrackCourses.Add(new CertificationTrackCourse { CourseId = courseId, IsRequired = true });

        _db.CertificationTracks.Add(track);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Track '{track.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var track = await _db.CertificationTracks
            .Include(t => t.CertificationTrackCourses)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (track == null) return NotFound();

        await PopulateCoursesAsync();
        return View(new CertificationTrackFormViewModel
        {
            Id = track.Id,
            Name = track.Name,
            Description = track.Description,
            CertRefPrefix = track.CertRefPrefix,
            SelectedCourseIds = track.CertificationTrackCourses.Select(c => c.CourseId).ToArray()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CertificationTrackFormViewModel model)
    {
        var track = await _db.CertificationTracks
            .Include(t => t.CertificationTrackCourses)
            .FirstOrDefaultAsync(t => t.Id == model.Id);
        if (track == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync();
            return View(model);
        }

        track.Name = model.Name.Trim();
        track.Description = model.Description?.Trim() ?? string.Empty;
        track.CertRefPrefix = model.CertRefPrefix?.Trim() ?? string.Empty;

        // Reconcile the required-course join rows against the selection.
        var selected = model.SelectedCourseIds.Distinct().ToHashSet();
        var existing = track.CertificationTrackCourses.ToList();

        foreach (var link in existing.Where(l => !selected.Contains(l.CourseId)))
            _db.CertificationTrackCourses.Remove(link);

        var current = existing.Select(l => l.CourseId).ToHashSet();
        foreach (var courseId in selected.Where(c => !current.Contains(c)))
            track.CertificationTrackCourses.Add(new CertificationTrackCourse { CourseId = courseId, IsRequired = true });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Track updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var track = await _db.CertificationTracks
            .Include(t => t.CertificationTrackCourses)
            .Include(t => t.TraineeCertifications)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (track == null) return NotFound();

        if (track.TraineeCertifications.Any())
        {
            TempData["Error"] = "Cannot delete a track that has trainee certificate records. Remove those first.";
            return RedirectToAction(nameof(Index));
        }

        _db.CertificationTrackCourses.RemoveRange(track.CertificationTrackCourses);
        _db.CertificationTracks.Remove(track);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Track deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCoursesAsync()
    {
        var courses = await _db.Courses
            .OrderBy(c => c.Title)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.Courses = new MultiSelectList(courses, "Id", "Title");
    }
}
