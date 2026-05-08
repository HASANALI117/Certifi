using System.Net.Http.Json;
using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Services;

public class CertificationLookupService : ICertificationLookupService
{
    private readonly HttpClient _httpClient;

    public CertificationLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CertificationVerifyResponse?> VerifyAsync(string traineeId, string certRef)
    {
        return await _httpClient.GetFromJsonAsync<CertificationVerifyResponse>(
            $"api/certifications/verify?traineeId={Uri.EscapeDataString(traineeId)}&certRef={Uri.EscapeDataString(certRef)}");
    }
}
