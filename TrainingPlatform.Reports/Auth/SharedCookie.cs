namespace TrainingPlatform.Reports.Auth;

// Reports shares its auth cookie with the MVC app so a single sign-in there
// flows into the reporting client. Both sides must use the same scheme name
// (ASP.NET Identity's "Identity.Application") because the data-protection
// purpose string is derived from it.
public static class SharedCookie
{
    public const string Scheme = "Identity.Application";
    public const string Name = "TrainingPlatform.Auth";
    public const string TokenClaimType = "ApiAccessToken";
}
