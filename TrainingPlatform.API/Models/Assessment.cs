using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public enum AssessmentResult
    {
        Pass, Fail
    }
    public class Assessment
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;

        public AssessmentResult Result { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public int RecordedById { get; set; }
        public Instructor RecordedBy { get; set; } = null!;
    }
}