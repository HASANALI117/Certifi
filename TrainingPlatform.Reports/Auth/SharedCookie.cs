namespace TrainingPlatform.Reports.Auth;

// Reports uses the same login cookie as the MVC app, so logging in once works for both. The scheme name has to match on both sides.
public static class SharedCookie
{
    public const string Scheme = "Identity.Application";
    public const string Name = "TrainingPlatform.Auth";
    public const string TokenClaimType = "ApiAccessToken";
}
