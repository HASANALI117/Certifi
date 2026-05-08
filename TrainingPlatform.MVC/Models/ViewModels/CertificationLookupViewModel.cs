using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class CertificationLookupViewModel
{
    [Required, Display(Name = "Trainee ID")]
    public string TraineeId { get; set; } = string.Empty;

    [Required, Display(Name = "Certificate Reference Number")]
    public string CertRef { get; set; } = string.Empty;

    public CertificationVerifyResponse? Result { get; set; }
    public bool Searched { get; set; }
    public string? ErrorMessage { get; set; }
}
