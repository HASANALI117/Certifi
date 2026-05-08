using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.API.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
    {
        public DbSet<Trainee> Trainees => Set<Trainee>();
        public DbSet<Instructor> Instructors => Set<Instructor>();
        public DbSet<InstructorAvailability> InstructorAvailability => Set<InstructorAvailability>();
        public DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Classroom> Classrooms => Set<Classroom>();
        public DbSet<ClassroomEquipment> ClassroomEquipment => Set<ClassroomEquipment>();
        public DbSet<CourseSession> CourseSessions => Set<CourseSession>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Assessment> Assessments => Set<Assessment>();
        public DbSet<CertificationTrack> CertificationTracks => Set<CertificationTrack>();
        public DbSet<CertificationTrackCourse> CertificationTrackCourses => Set<CertificationTrackCourse>();
        public DbSet<TraineeCertification> TraineeCertifications => Set<TraineeCertification>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Notification> Notifications => Set<Notification>();


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ── AppUser one-to-one profiles ──────────────────────────────────────
            builder.Entity<AppUser>()
                .HasOne(u => u.TraineeProfile)
                .WithOne(t => t.User)
                .HasForeignKey<Trainee>(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AppUser>()
                .HasOne(u => u.InstructorProfile)
                .WithOne(i => i.User)
                .HasForeignKey<Instructor>(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Trainee public ID must be unique (used for the public lookup page) ──
            builder.Entity<Trainee>()
                .HasIndex(t => t.TraineePublicId)
                .IsUnique();

            // ── Course self-referencing prerequisite ─────────────────────────────
            // NoAction prevents EF from trying to cascade-delete a prerequisite course
            // while child courses still reference it
            builder.Entity<Course>()
                .HasOne(c => c.PrerequisiteCourse)
                .WithMany()
                .HasForeignKey(c => c.PrerequisiteCourseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Course>()
                .Property(c => c.EnrollmentFee)
                .HasColumnType("decimal(10,2)");

            // ── CertificationTrackCourse composite PK ────────────────────────────
            // EF won't know the PK for this junction table without being told explicitly
            builder.Entity<CertificationTrackCourse>()
                .HasKey(ctc => new { ctc.CertificationTrackId, ctc.CourseId });

            builder.Entity<CertificationTrackCourse>()
                .HasOne(ctc => ctc.CertificationTrack)
                .WithMany(ct => ct.CertificationTrackCourses)
                .HasForeignKey(ctc => ctc.CertificationTrackId);

            builder.Entity<CertificationTrackCourse>()
                .HasOne(ctc => ctc.Course)
                .WithMany(c => c.CertificationTrackCourses)
                .HasForeignKey(ctc => ctc.CourseId);

            // ── Assessment → Instructor (recorder) ──────────────────────────────
            // NoAction because this FK goes Instructor → Assessment but Instructor
            // also has a cascade from AppUser. SQL Server won't allow two cascade
            // paths to the same table.
            builder.Entity<Assessment>()
                .HasOne(a => a.RecordedBy)
                .WithMany()
                .HasForeignKey(a => a.RecordedById)
                .OnDelete(DeleteBehavior.NoAction);

            // ── Payment decimal precision ─────────────────────────────────────────
            builder.Entity<Payment>()
                .Property(p => p.AmountPaid)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Payment>()
                .Property(p => p.OutstandingBalance)
                .HasColumnType("decimal(10,2)");

            // ── Unique index: prevent double-booking a session slot ───────────────
            // An instructor can't be in two sessions at the same start time
            builder.Entity<CourseSession>()
                .HasIndex(cs => new { cs.InstructorId, cs.StartDateTime })
                .IsUnique();

            // A classroom can't host two sessions at the same start time
            builder.Entity<CourseSession>()
                .HasIndex(cs => new { cs.ClassroomId, cs.StartDateTime })
                .IsUnique();

            // ── Unique index: a trainee can only enroll once per session ──────────
            builder.Entity<Enrollment>()
                .HasIndex(e => new { e.TraineeId, e.CourseSessionId })
                .IsUnique();

            // ── Notifications FK ─────────────────────────────────────────────────
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction — SQL Server blocks multiple cascade paths to Enrollments
            // (AppUser→Trainee→Enrollment and AppUser→Instructor→CourseSession→Enrollment)
            builder.Entity<Enrollment>()
                .HasOne(e => e.Trainee)
                .WithMany(t => t.Enrollments)
                .HasForeignKey(e => e.TraineeId)
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction — SQL Server blocks multiple cascade paths to Enrollments
            builder.Entity<Enrollment>()
                .HasOne(e => e.CourseSession)
                .WithMany(cs => cs.Enrollments)
                .HasForeignKey(e => e.CourseSessionId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}