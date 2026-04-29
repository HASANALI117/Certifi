using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrainingPlatform.API.Models
{
    public class CertificationTrackCourse
    {
        public int CertificationTrackId { get; set; }
        public CertificationTrack CertificationTrack { get; set; } = null!;

        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public bool IsRequired { get; set; } = true;
    }
}