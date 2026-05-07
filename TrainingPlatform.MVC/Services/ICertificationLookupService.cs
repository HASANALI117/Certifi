using TrainingPlatform.MVC.Models.ViewModels;

namespace TrainingPlatform.MVC.Services;

public interface ICertificationLookupService
{
    Task<CertificationVerifyResponse?> VerifyAsync(string traineeId, string certRef);
}
