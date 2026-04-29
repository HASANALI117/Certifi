using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;

        public decimal AmountPaid { get; set; }
        public DateTime PaidAt { get; set; }
        public decimal OutstandingBalance { get; set; }
    }
}