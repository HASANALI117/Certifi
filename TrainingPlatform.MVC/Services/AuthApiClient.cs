using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrainingPlatform.MVC.Services;

public class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AuthApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuthApiClient(HttpClient http, ILogger<AuthApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password },
                JsonOptions,
                ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.BadRequest)
                return null;

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<LoginResponsePayload>(JsonOptions, ct);
            return body?.Token;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "API login request failed.");
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "API login request timed out.");
            return null;
        }
    }

    private sealed record LoginResponsePayload(string Token);
}
