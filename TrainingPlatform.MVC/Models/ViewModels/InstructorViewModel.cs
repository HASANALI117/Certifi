using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class InstructorListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ExpertiseAreas { get; set; } = string.Empty;
    public int SessionCount { get; set; }
}

public class InstructorFormViewModel
{
    public int Id { get; set; }

    // True on edit — account fields are hidden and not validated.
    public bool IsEdit => Id != 0;

    // These account fields are only used when creating. The controller checks them by hand so Edit can share this model.
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Temporary Password")]
    public string TempPassword { get; set; } = string.Empty;

    [Required, StringLength(500), Display(Name = "Expertise Areas")]
    public string ExpertiseAreas { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Bio { get; set; } = string.Empty;
}

public class InstructorDetailsViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ExpertiseAreas { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public List<string> UpcomingSessions { get; set; } = new();
}
