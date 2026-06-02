using TrainingPlatform.API.Models;

namespace TrainingPlatform.MVC.Models.ViewModels;

// Coordinator/Instructor "Manage Enrollments" page. Bundles the enrollment list
// with the derived payment-status map.
public class ManageEnrollmentsViewModel
{
    public IReadOnlyList<Enrollment> Enrollments { get; set; } = [];
    public IReadOnlyDictionary<int, string> PaymentStatuses { get; set; } = new Dictionary<int, string>();
}

// Trainee "My Enrollment Dashboard" (BillingAndAlerts) page. Replaces the four
// ViewBag entries (payment statuses, outstanding balances, notifications,
// certifications) that the view previously had to cast back out of ViewBag.
public class TraineeBillingViewModel
{
    public IReadOnlyList<Enrollment> Enrollments { get; set; } = [];
    public IReadOnlyDictionary<int, string> PaymentStatuses { get; set; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, decimal> OutstandingBalances { get; set; } = new Dictionary<int, decimal>();
    public IReadOnlyList<Notification> Notifications { get; set; } = [];
    public IReadOnlyList<TraineeCertification> Certifications { get; set; } = [];
}
