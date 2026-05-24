using Microsoft.AspNetCore.Mvc;

namespace TrainingPlatform.MVC.Infrastructure;

public static class ControllerRedirectExtensions
{
    // Open-redirect-safe alternative to Redirect(returnUrl).
    // Returns LocalRedirect when returnUrl is a local URL, otherwise falls back
    // to the supplied default action.
    //
    // Why this exists: every place that consumes a returnUrl query parameter
    // must call Url.IsLocalUrl before redirecting, otherwise an attacker can
    // craft a link that bounces the user off-domain after login.
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
