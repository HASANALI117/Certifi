using TrainingPlatform.API.Models;

namespace TrainingPlatform.MVC.Models.ViewModels;

// Data for the Manage Enrollments page: the enrollments and their payment status.
public class ManageEnrollmentsViewModel
{
    public IReadOnlyList<Enrollment> Enrollments { get; set; } = [];
    public IReadOnlyDictionary<int, string> PaymentStatuses { get; set; } = new Dictionary<int, string>();
}

// Data for the instructor's assessment roster: enrollments only, no payment info.
public class InstructorRosterViewModel
{
    public IReadOnlyList<Enrollment> Enrollments { get; set; } = [];
}

// Data for the trainee's billing page: payment status, balances, notifications, and certificates.
public class TraineeBillingViewModel
{
    public IReadOnlyList<Enrollment> Enrollments { get; set; } = [];
    public IReadOnlyDictionary<int, string> PaymentStatuses { get; set; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, decimal> OutstandingBalances { get; set; } = new Dictionary<int, decimal>();
    public IReadOnlyList<Notification> Notifications { get; set; } = [];
    public IReadOnlyList<TraineeCertification> Certifications { get; set; } = [];
}
