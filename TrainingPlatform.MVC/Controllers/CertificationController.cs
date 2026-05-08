using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.MVC.Models.ViewModels;
using TrainingPlatform.MVC.Services;

namespace TrainingPlatform.MVC.Controllers;

public class CertificationController : Controller
{
    private readonly ICertificationLookupService _lookupService;

    public CertificationController(ICertificationLookupService lookupService)
    {
        _lookupService = lookupService;
    }

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
}
