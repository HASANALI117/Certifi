using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

[Authorize(Roles = "TrainingCoordinator")]
public class ClassroomsController : Controller
{
    private readonly AppDbContext _db;

    public ClassroomsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var classrooms = await _db.Classrooms
            .Include(c => c.Equipment)
            .Include(c => c.CourseSessions)
            .Select(c => new ClassroomViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Capacity = c.Capacity,
                Equipment = string.Join(", ", c.Equipment.Select(e => e.EquipmentName)),
                SessionCount = c.CourseSessions.Count
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
            Equipment = BuildEquipment(model.Equipment)
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Classroom '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await _db.Classrooms
            .Include(c => c.Equipment)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (room == null) return NotFound();

        return View(new ClassroomViewModel
        {
            Id = room.Id,
            Name = room.Name,
            Capacity = room.Capacity,
            Equipment = string.Join(", ", room.Equipment.Select(e => e.EquipmentName))
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClassroomViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var room = await _db.Classrooms
            .Include(c => c.Equipment)
            .FirstOrDefaultAsync(c => c.Id == model.Id);
        if (room == null) return NotFound();

        room.Name = model.Name;
        room.Capacity = model.Capacity;
        _db.ClassroomEquipment.RemoveRange(room.Equipment);
        room.Equipment = BuildEquipment(model.Equipment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Classroom updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _db.Classrooms.Include(c => c.CourseSessions).FirstOrDefaultAsync(c => c.Id == id);
        if (room == null) return NotFound();

        if (room.CourseSessions.Any())
        {
            TempData["Error"] = "Cannot delete a classroom that has scheduled sessions.";
            return RedirectToAction(nameof(Index));
        }

        _db.Classrooms.Remove(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Classroom deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static List<ClassroomEquipment> BuildEquipment(string equipment) =>
        equipment
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => new ClassroomEquipment { EquipmentName = name })
            .ToList();
}
