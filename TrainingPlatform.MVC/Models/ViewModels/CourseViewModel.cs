using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using TrainingPlatform.MVC.Models.Validation;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class CourseListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public int Capacity { get; set; }
    public decimal Fee { get; set; }
    public string? PrerequisiteTitle { get; set; }
    public string? ImageUrl { get; set; }
    public int SessionCount { get; set; }
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

    [Required]
    [Range(1.000, 100000.000, ErrorMessage = "Fee must be at least BD 1.000.")]
    [DataType(DataType.Currency)]
    public decimal Fee { get; set; }

    [Required, Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Prerequisite Course")]
    public int? PrerequisiteCourseId { get; set; }

    [Display(Name = "Course Image URL")]
    [RelativeOrAbsoluteUrl(ErrorMessage = "Enter a valid URL (https://… or /images/…).")]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Upload Image")]
    public IFormFile? ImageFile { get; set; }

    public string? ExistingImageUrl { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Courses { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class UpcomingSessionWidgetViewModel
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
}

public class InstructorWidgetViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string ExpertiseAreas { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
}

public class CategoryWidgetViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CourseCount { get; set; }
}
