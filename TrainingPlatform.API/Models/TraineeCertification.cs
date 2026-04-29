using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public enum CertificationStatus
    {
        InProgress, Eligible, Issued
    }
    public class TraineeCertification
    {
        public int Id { get; set; }
        public int TraineeId { get; set; }
        public Trainee Trainee { get; set; } = null!;

        public int CertificationTrackId { get; set; }
        public CertificationTrack CertificationTrack { get; set; } = null!;

        public CertificationStatus Status { get; set; } = CertificationStatus.InProgress;
        public string CertRefNumber { get; set; } = string.Empty;
        public DateTime? IssuedAt { get; set; }
    }
}