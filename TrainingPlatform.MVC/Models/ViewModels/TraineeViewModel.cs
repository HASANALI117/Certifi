using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.ViewModels;

public class TraineeListItemViewModel
{
    public int Id { get; set; }
    public string TraineePublicId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
}

public class TraineeFormViewModel
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

    [Phone, Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Date of Birth")]
    public DateOnly DateOfBirth { get; set; }
}

public class TraineeDetailsViewModel
{
    public int Id { get; set; }
    public string TraineePublicId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public List<string> UpcomingEnrollments { get; set; } = new();
}
