namespace TrainingPlatform.MVC.Services;

public interface IAuthApiClient
{
    // Returns the JWT string on success, null on bad credentials, throws on
    // transport failure so callers can degrade gracefully.
    Task<string?> LoginAsync(string email, string password, CancellationToken ct = default);
}
