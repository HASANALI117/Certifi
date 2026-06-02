using System.ComponentModel.DataAnnotations;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.MVC.Models.ViewModels;

// Coordinator-facing form for a single trainee certificate record
// (TraineeCertification). Select lists for Trainee/Track are supplied via
// ViewBag by the controller, mirroring the EnrollmentsController pattern.
public class TraineeCertificationFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please choose a trainee.")]
    [Display(Name = "Trainee")]
    public int TraineeId { get; set; }

    [Required(ErrorMessage = "Please choose a certification track.")]
    [Display(Name = "Certification track")]
    public int CertificationTrackId { get; set; }

    [Display(Name = "Status")]
    public CertificationStatus Status { get; set; } = CertificationStatus.InProgress;

    [Display(Name = "Reference number")]
    [StringLength(64)]
    public string? CertRefNumber { get; set; }

    [Display(Name = "Issued date")]
    [DataType(DataType.Date)]
    public DateTime? IssuedAt { get; set; }
}

// Coordinator-facing form for a certification track template (CertificationTrack)
// plus the set of required courses (CertificationTrackCourse). The full course
// list is supplied via ViewBag.Courses.
public class CertificationTrackFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120)]
    [Display(Name = "Track name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(16)]
    [Display(Name = "Reference prefix")]
    public string? CertRefPrefix { get; set; }

    [Display(Name = "Required courses")]
    public int[] SelectedCourseIds { get; set; } = [];
}
