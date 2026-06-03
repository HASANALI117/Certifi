using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Models.ViewModels;
using TrainingPlatform.MVC.Services;

namespace TrainingPlatform.MVC.Controllers;

public class CertificationController(ICertificationLookupService lookupService, AppDbContext db) : Controller
{
    private readonly ICertificationLookupService _lookupService = lookupService;
    private readonly AppDbContext _db = db;

    // Public page for checking a certificate. Only linked from the home page and open to everyone.
    [HttpGet]
    public IActionResult Lookup() => View(new CertificationLookupViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup(CertificationLookupViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.Searched = true;

        try
        {
            model.Result = await _lookupService.VerifyAsync(model.TraineeId, model.CertRef);

            if (model.Result == null)
                model.ErrorMessage = "No certification record found for the provided Trainee ID and Certificate Reference.";
        }
        catch (HttpRequestException)
        {
            model.ErrorMessage = "The verification service is currently unavailable. Please try again later.";
        }

        return View(model);
    }

    // Shows the logged-in trainee their own certificates right away, no search needed.
    [Authorize(Roles = "Trainee")]
    [HttpGet]
    public async Task<IActionResult> MyCertificate()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var certifications = await _db.TraineeCertifications
            .Include(c => c.CertificationTrack)
            .Where(c => c.Trainee.UserId == userId)
            .OrderByDescending(c => c.Status == CertificationStatus.Issued)
            .ThenBy(c => c.CertificationTrack.Name)
            .ToListAsync();

        return View(certifications);
    }
}
