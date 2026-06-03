using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TrainingPlatform.Reports.Auth;
using TrainingPlatform.Reports.Services;

namespace TrainingPlatform.Reports.Controllers;

[Authorize(Policy = "TrainingCoordinator")]
public abstract class ReportControllerBase : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            await base.OnActionExecutionAsync(context, next);
        }
        catch (ApiUnauthorizedException)
        {
            await HttpContext.SignOutAsync(SharedCookie.Scheme);
            var navLinks = HttpContext.RequestServices.GetRequiredService<INavLinks>();
            context.Result = Redirect(navLinks.Mvc("/Account/Login"));
        }
        catch (ApiUnavailableException)
        {
            context.Result = RedirectToAction("ServiceUnavailable", "Home");
        }
    }
}
