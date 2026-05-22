using System.Net.Http.Json;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Services;

public class CertificationLookupService(HttpClient httpClient) : ICertificationLookupService
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<CertificationVerifyResponse?> VerifyAsync(string traineeId, string certRef)
    {
        var response = await _httpClient.GetAsync(
            $"api/certifications/verify?traineeId={Uri.EscapeDataString(traineeId)}&certRef={Uri.EscapeDataString(certRef)}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CertificationVerifyResponse>();
    }
}
