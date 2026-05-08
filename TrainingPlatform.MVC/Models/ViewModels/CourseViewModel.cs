using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class CourseListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public int Capacity { get; set; }
    public decimal Fee { get; set; }
    public string? PrerequisiteTitle { get; set; }
}

public class CourseFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, Range(1, 1000), Display(Name = "Duration (hours)")]
    public int DurationHours { get; set; }

    [Required, Range(1, 500)]
    public int Capacity { get; set; }

    [Required, Range(0, 100000), DataType(DataType.Currency)]
    public decimal Fee { get; set; }

    [Required, Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Prerequisite Course")]
    public int? PrerequisiteCourseId { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Courses { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class CourseDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public int Capacity { get; set; }
    public decimal Fee { get; set; }
    public string? PrerequisiteTitle { get; set; }
    public int SessionCount { get; set; }
}
