namespace TrainingPlatform.MVC.Services;

public interface IAuthApiClient
{
    // Returns the token if login works, null if the password's wrong, and throws if it can't reach the API.
    Task<string?> LoginAsync(string email, string password, CancellationToken ct = default);
}
