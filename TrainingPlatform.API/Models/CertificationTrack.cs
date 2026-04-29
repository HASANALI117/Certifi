using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class CertificationTrack
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CertRefPrefix { get; set; } = string.Empty;

        public ICollection<CertificationTrackCourse> CertificationTrackCourses { get; set; } = [];
        public ICollection<TraineeCertification> TraineeCertifications { get; set; } = [];
    }
}