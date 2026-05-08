using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class CategoryViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public int CourseCount { get; set; }
}
