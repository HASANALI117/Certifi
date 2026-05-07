using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Entities;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "Training Coordinator")]
public class ClassroomsController : Controller
{
    private readonly TrainingPlatformDbContext _db;

    public ClassroomsController(TrainingPlatformDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var classrooms = await _db.Classrooms
            .Include(c => c.Sessions)
            .Select(c => new ClassroomViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Capacity = c.Capacity,
                Equipment = c.Equipment,
                SessionCount = c.Sessions.Count
            })
            .ToListAsync();

        return View(classrooms);
    }

    [HttpGet]
    public IActionResult Create() => View(new ClassroomViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClassroomViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _db.Classrooms.Add(new Classroom
        {
            Name = model.Name,
            Capacity = model.Capacity,
            Equipment = model.Equipment
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Classroom '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await _db.Classrooms.FindAsync(id);
        if (room == null) return NotFound();

        return View(new ClassroomViewModel
        {
            Id = room.Id,
            Name = room.Name,
            Capacity = room.Capacity,
            Equipment = room.Equipment
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClassroomViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var room = await _db.Classrooms.FindAsync(model.Id);
        if (room == null) return NotFound();

        room.Name = model.Name;
        room.Capacity = model.Capacity;
        room.Equipment = model.Equipment;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Classroom updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _db.Classrooms.Include(c => c.Sessions).FirstOrDefaultAsync(c => c.Id == id);
        if (room == null) return NotFound();

        if (room.Sessions.Any())
        {
            TempData["Error"] = "Cannot delete a classroom that has scheduled sessions.";
            return RedirectToAction(nameof(Index));
        }

        _db.Classrooms.Remove(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Classroom deleted.";
        return RedirectToAction(nameof(Index));
    }
}
