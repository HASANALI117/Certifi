using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class ClassroomViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1, 1000)]
    public int Capacity { get; set; }

    [StringLength(500), Display(Name = "Equipment")]
    public string Equipment { get; set; } = string.Empty;

    public int SessionCount { get; set; }
}
