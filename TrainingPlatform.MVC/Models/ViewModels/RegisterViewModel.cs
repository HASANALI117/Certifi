using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class RegisterViewModel
{
    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    public List<string> AvailableRoles { get; set; } = new() { "Trainee", "Instructor" };
}
