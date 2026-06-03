using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Controllers;

// Only the coordinator manages trainee certificates here, including issuing them with a reference number.
[Authorize(Roles = "TrainingCoordinator")]
public class TraineeCertificationsController : Controller
{
    private readonly AppDbContext _db;

    public TraineeCertificationsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var certifications = await _db.TraineeCertifications
            .Include(c => c.Trainee).ThenInclude(t => t.User)
            .Include(c => c.CertificationTrack)
            .OrderBy(c => c.Status)
            .ThenBy(c => c.CertificationTrack.Name)
            .ToListAsync();

        return View(certifications);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateSelectListsAsync();
        return View(new TraineeCertificationFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TraineeCertificationFormViewModel model)
    {
        await ValidateCreateAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(model.TraineeId, model.CertificationTrackId);
            return View(model);
        }

        _db.TraineeCertifications.Add(new TraineeCertification
        {
            TraineeId = model.TraineeId,
            CertificationTrackId = model.CertificationTrackId,
            Status = model.Status,
            CertRefNumber = model.CertRefNumber?.Trim() ?? string.Empty,
            IssuedAt = model.Status == CertificationStatus.Issued
                ? (model.IssuedAt ?? DateTime.UtcNow)
                : model.IssuedAt
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Trainee certificate created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var cert = await _db.TraineeCertifications.FindAsync(id);
        if (cert == null) return NotFound();

        await PopulateSelectListsAsync(cert.TraineeId, cert.CertificationTrackId);
        return View(new TraineeCertificationFormViewModel
        {
            Id = cert.Id,
            TraineeId = cert.TraineeId,
            CertificationTrackId = cert.CertificationTrackId,
            Status = cert.Status,
            CertRefNumber = cert.CertRefNumber,
            IssuedAt = cert.IssuedAt
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TraineeCertificationFormViewModel model)
    {
        var cert = await _db.TraineeCertifications.FindAsync(model.Id);
        if (cert == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(model.TraineeId, model.CertificationTrackId);
            return View(model);
        }

        cert.TraineeId = model.TraineeId;
        cert.CertificationTrackId = model.CertificationTrackId;
        cert.Status = model.Status;
        cert.CertRefNumber = model.CertRefNumber?.Trim() ?? string.Empty;
        cert.IssuedAt = model.Status == CertificationStatus.Issued
            ? (model.IssuedAt ?? DateTime.UtcNow)
            : model.IssuedAt;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Trainee certificate updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var cert = await _db.TraineeCertifications.FindAsync(id);
        if (cert == null) return NotFound();

        _db.TraineeCertifications.Remove(cert);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Trainee certificate deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id)
    {
        var cert = await _db.TraineeCertifications
            .Include(c => c.CertificationTrack)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cert == null) return NotFound();

        cert.Status = CertificationStatus.Issued;
        cert.IssuedAt = DateTime.UtcNow;

        // Save first so the new id can go into the reference number, then set the reference if it doesn't have one.
        if (string.IsNullOrWhiteSpace(cert.CertRefNumber))
        {
            await _db.SaveChangesAsync();
            cert.CertRefNumber = BuildCertificateReference(cert);
        }

        _db.Notifications.Add(new Notification
        {
            UserId = await _db.Trainees.Where(t => t.Id == cert.TraineeId)
                .Select(t => t.UserId).FirstOrDefaultAsync() ?? string.Empty,
            Message = $"Certification issued: {cert.CertificationTrack.Name}. Reference: {cert.CertRefNumber}.",
            Type = "Certification",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Certificate issued.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCreateAsync(TraineeCertificationFormViewModel model)
    {
        var duplicate = await _db.TraineeCertifications.AnyAsync(c =>
            c.TraineeId == model.TraineeId && c.CertificationTrackId == model.CertificationTrackId);

        if (duplicate)
            ModelState.AddModelError(string.Empty, "This trainee already has a certificate record for that track.");
    }

    private async Task PopulateSelectListsAsync(int? traineeId = null, int? trackId = null)
    {
        var trainees = await _db.Trainees
            .Include(t => t.User)
            .OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName)
            .ToListAsync();

        ViewBag.TraineeId = new SelectList(
            trainees.Select(t => new { t.Id, Name = $"{t.User.FirstName} {t.User.LastName} ({t.TraineePublicId})" }),
            "Id", "Name", traineeId);

        var tracks = await _db.CertificationTracks
            .OrderBy(t => t.Name)
            .ToListAsync();

        ViewBag.CertificationTrackId = new SelectList(tracks, "Id", "Name", trackId);
    }

    private static string BuildCertificateReference(TraineeCertification certification)
    {
        var prefix = string.IsNullOrWhiteSpace(certification.CertificationTrack.CertRefPrefix)
            ? "CERT"
            : certification.CertificationTrack.CertRefPrefix.Trim();

        return $"{prefix}-{DateTime.UtcNow:yyyy}-{certification.Id:D5}";
    }
}
