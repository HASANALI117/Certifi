using Microsoft.AspNetCore.Mvc;

namespace TrainingPlatform.MVC.Infrastructure;

public static class ControllerRedirectExtensions
{
    // Only redirect to local URLs. This stops someone using a link to send the user to another site after login.
    public static IActionResult SafeLocalRedirect(
        this Controller controller,
        string? returnUrl,
        string fallbackAction,
        string fallbackController)
    {
        if (!string.IsNullOrEmpty(returnUrl) && controller.Url.IsLocalUrl(returnUrl))
            return controller.LocalRedirect(returnUrl);

        return controller.RedirectToAction(fallbackAction, fallbackController);
    }
}
