using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class CourseSessionListItemViewModel
{
    public int Id { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public int AvailableSpots { get; set; }
    public int EnrollmentCount { get; set; }
}

public class CourseSessionFormViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "Course")]
    public int CourseId { get; set; }

    [Required, Display(Name = "Instructor")]
    public int InstructorId { get; set; }

    [Required, Display(Name = "Classroom")]
    public int ClassroomId { get; set; }

    [Required, Display(Name = "Session Date"), DataType(DataType.Date)]
    public DateOnly SessionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    [Required, Display(Name = "Start Time"), DataType(DataType.Time)]
    public TimeOnly StartTime { get; set; } = new TimeOnly(9, 0);

    [Required, Range(1, 500), Display(Name = "Available Spots")]
    public int AvailableSpots { get; set; }

    public IEnumerable<SelectListItem> Courses { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Instructors { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Classrooms { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class CourseSessionDetailsViewModel
{
    public int Id { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseDescription { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string ClassroomEquipment { get; set; } = string.Empty;
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public int AvailableSpots { get; set; }
    public int EnrollmentCount { get; set; }
}
