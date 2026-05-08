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

    [Required, Display(Name = "User Account")]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(500), Display(Name = "Expertise Areas")]
    public string ExpertiseAreas { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Bio { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> AvailableUsers { get; set; } = Enumerable.Empty<SelectListItem>();
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
