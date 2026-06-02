using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.API.Data;

public class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager, context);
        await SeedCatalogAsync(context);
        await SeedActivityAsync(context);
        await BackfillCourseImagesAsync(context);
    }

    // Maps the seeded course titles to images shipped in wwwroot/images.
    // Runs every startup so it also fixes databases that were seeded before
    // the ImageUrl column existed. Only writes when the column is empty so
    // it never overrides an image set through the UI.
    private static async Task BackfillCourseImagesAsync(AppDbContext context)
    {
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Networking Fundamentals"] = "/images/Networking_Fundementals.jpg",
            ["Advanced Networking"]     = "/images/Advanced_Networking.jpg",
            ["Cloud Essentials"]        = "/images/Cloud_Essentials.jpg",
            ["AWS Solutions Architect"] = "/images/AwsSolutionsArchitect.png",
            ["Security Fundamentals"]   = "/images/Security_Fundementals.jpg"
        };

        var courses = await context.Courses
            .Where(c => c.ImageUrl == null || c.ImageUrl == "")
            .ToListAsync();

        var changed = false;
        foreach (var course in courses)
        {
            if (defaults.TryGetValue(course.Title, out var url))
            {
                course.ImageUrl = url;
                changed = true;
            }
        }

        if (changed) await context.SaveChangesAsync();
    }

    // ── Roles ─────────────────────────────────────────────────────────────────
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = ["TrainingCoordinator", "Instructor", "Trainee"];
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ── Users (coordinators, instructors, trainees) ───────────────────────────
    private static async Task SeedUsersAsync(
        UserManager<AppUser> userManager, AppDbContext context)
    {
        await EnsureCoordinator(userManager, "coordinator@platform.com", "Sarah", "Mitchell");

        await EnsureInstructor(userManager, context,
            email: "instructor@platform.com",
            first: "James", last: "Carter",
            bio: "Senior network engineer with 10 years of industry experience.",
            expertise: "Networking, TCP/IP, Security",
            availability:
            [
                new(DayOfWeek.Monday,    new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0))
            ]);

        await EnsureInstructor(userManager, context,
            email: "maria@platform.com",
            first: "Maria", last: "Rodriguez",
            bio: "Cloud architect specializing in AWS and Azure migrations.",
            expertise: "AWS, Azure, DevOps",
            availability:
            [
                new(DayOfWeek.Tuesday,  new TimeOnly(10, 0), new TimeOnly(18, 0)),
                new(DayOfWeek.Thursday, new TimeOnly(10, 0), new TimeOnly(18, 0))
            ]);

        await EnsureTrainee(userManager, context,
            email: "trainee@platform.com", first: "Ali", last: "Hassan",
            publicId: "TRN-0001", phone: "+97312345678",
            dob: new DateOnly(2000, 5, 15));

        await EnsureTrainee(userManager, context,
            email: "layla@platform.com", first: "Layla", last: "Khan",
            publicId: "TRN-0002", phone: "+97312345679",
            dob: new DateOnly(1998, 11, 3));

        await EnsureTrainee(userManager, context,
            email: "omar@platform.com", first: "Omar", last: "Said",
            publicId: "TRN-0003", phone: "+97312345680",
            dob: new DateOnly(1995, 2, 22));

        await context.SaveChangesAsync();
    }

    // ── Catalog (categories, courses, classrooms, tracks, sessions) ───────────
    private static async Task SeedCatalogAsync(AppDbContext context)
    {
        if (context.CourseCategories.Any()) return;

        // categories
        var networking = new CourseCategory { Name = "Networking", Description = "Network infrastructure and protocols" };
        var cloud = new CourseCategory { Name = "Cloud Computing", Description = "Cloud platforms and services" };
        var security = new CourseCategory { Name = "Cybersecurity", Description = "Defensive security and threat response" };
        context.CourseCategories.AddRange(networking, cloud, security);
        await context.SaveChangesAsync();

        // courses
        var fundamentals = new Course
        {
            CategoryId = networking.Id,
            Title = "Networking Fundamentals",
            Description = "Core concepts of networking including TCP/IP and subnetting.",
            DurationHours = 20,
            Capacity = 20,
            EnrollmentFee = 150.00m,
            ImageUrl = "/images/Networking_Fundementals.jpg"
        };
        context.Courses.Add(fundamentals);
        await context.SaveChangesAsync();

        var advanced = new Course
        {
            CategoryId = networking.Id,
            PrerequisiteCourseId = fundamentals.Id,
            Title = "Advanced Networking",
            Description = "Advanced routing, switching, and network security.",
            DurationHours = 30,
            Capacity = 15,
            EnrollmentFee = 250.00m,
            ImageUrl = "/images/Advanced_Networking.jpg"
        };
        var cloudEssentials = new Course
        {
            CategoryId = cloud.Id,
            Title = "Cloud Essentials",
            Description = "Introduction to cloud computing concepts and AWS basics.",
            DurationHours = 25,
            Capacity = 20,
            EnrollmentFee = 200.00m,
            ImageUrl = "/images/Cloud_Essentials.jpg"
        };
        var awsArchitect = new Course
        {
            CategoryId = cloud.Id,
            PrerequisiteCourseId = null,
            Title = "AWS Solutions Architect",
            Description = "Design and deploy scalable systems on AWS.",
            DurationHours = 40,
            Capacity = 15,
            EnrollmentFee = 400.00m,
            ImageUrl = "/images/AwsSolutionsArchitect.png"
        };
        var secFundamentals = new Course
        {
            CategoryId = security.Id,
            Title = "Security Fundamentals",
            Description = "Threat modelling, OWASP top 10, and defensive coding.",
            DurationHours = 24,
            Capacity = 18,
            EnrollmentFee = 180.00m,
            ImageUrl = "/images/Security_Fundementals.jpg"
        };
        context.Courses.AddRange(advanced, cloudEssentials, awsArchitect, secFundamentals);
        await context.SaveChangesAsync();

        // wire AWS prereq once Cloud Essentials has an Id
        awsArchitect.PrerequisiteCourseId = cloudEssentials.Id;
        await context.SaveChangesAsync();

        // classrooms
        var labA = new Classroom
        {
            Name = "Lab A",
            Capacity = 20,
            Equipment =
            [
                new() { EquipmentName = "Projector" },
                new() { EquipmentName = "Lab Computers" }
            ]
        };
        var labB = new Classroom
        {
            Name = "Lab B",
            Capacity = 15,
            Equipment =
            [
                new() { EquipmentName = "Whiteboard" },
                new() { EquipmentName = "Conference Phone" }
            ]
        };
        context.Classrooms.AddRange(labA, labB);
        await context.SaveChangesAsync();

        // certification tracks
        var netTrack = new CertificationTrack
        {
            Name = "Certified Network Professional",
            Description = "Complete both networking courses to earn this certification.",
            CertRefPrefix = "CERT-NET"
        };
        var cloudTrack = new CertificationTrack
        {
            Name = "Certified Cloud Practitioner",
            Description = "Complete the cloud essentials and AWS architect courses.",
            CertRefPrefix = "CERT-CLD"
        };
        var secTrack = new CertificationTrack
        {
            Name = "Certified Security Associate",
            Description = "Pass the security fundamentals course.",
            CertRefPrefix = "CERT-SEC"
        };
        context.CertificationTracks.AddRange(netTrack, cloudTrack, secTrack);
        await context.SaveChangesAsync();

        context.CertificationTrackCourses.AddRange(
            new() { CertificationTrackId = netTrack.Id, CourseId = fundamentals.Id, IsRequired = true },
            new() { CertificationTrackId = netTrack.Id, CourseId = advanced.Id, IsRequired = true },
            new() { CertificationTrackId = cloudTrack.Id, CourseId = cloudEssentials.Id, IsRequired = true },
            new() { CertificationTrackId = cloudTrack.Id, CourseId = awsArchitect.Id, IsRequired = true },
            new() { CertificationTrackId = secTrack.Id, CourseId = secFundamentals.Id, IsRequired = true }
        );
        await context.SaveChangesAsync();

        // sessions — mix of past (Completed), current (InProgress), future (Scheduled)
        // unique constraints: (InstructorId, StartDateTime) and (ClassroomId, StartDateTime)
        var james = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "instructor@platform.com");
        var maria = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "maria@platform.com");

        var now = DateTime.UtcNow;

        context.CourseSessions.AddRange(
            // — completed —
            new CourseSession
            {
                CourseId = fundamentals.Id,
                InstructorId = james.Id,
                ClassroomId = labA.Id,
                StartDateTime = now.AddDays(-45).Date.AddHours(9),
                EndDateTime = now.AddDays(-45).Date.AddHours(13),
                Capacity = 20,
                Status = SessionStatus.Completed
            },
            new CourseSession
            {
                CourseId = advanced.Id,
                InstructorId = james.Id,
                ClassroomId = labA.Id,
                StartDateTime = now.AddDays(-20).Date.AddHours(9),
                EndDateTime = now.AddDays(-20).Date.AddHours(13),
                Capacity = 15,
                Status = SessionStatus.Completed
            },
            // — in progress —
            new CourseSession
            {
                CourseId = cloudEssentials.Id,
                InstructorId = maria.Id,
                ClassroomId = labB.Id,
                StartDateTime = now.AddDays(-1).Date.AddHours(10),
                EndDateTime = now.AddDays(2).Date.AddHours(14),
                Capacity = 20,
                Status = SessionStatus.InProgress
            },
            // — scheduled —
            new CourseSession
            {
                CourseId = fundamentals.Id,
                InstructorId = james.Id,
                ClassroomId = labA.Id,
                StartDateTime = now.AddDays(7).Date.AddHours(9),
                EndDateTime = now.AddDays(7).Date.AddHours(13),
                Capacity = 20,
                Status = SessionStatus.Scheduled
            },
            new CourseSession
            {
                CourseId = awsArchitect.Id,
                InstructorId = maria.Id,
                ClassroomId = labB.Id,
                StartDateTime = now.AddDays(14).Date.AddHours(10),
                EndDateTime = now.AddDays(14).Date.AddHours(15),
                Capacity = 15,
                Status = SessionStatus.Scheduled
            },
            new CourseSession
            {
                CourseId = secFundamentals.Id,
                InstructorId = james.Id,
                ClassroomId = labB.Id,
                StartDateTime = now.AddDays(21).Date.AddHours(9),
                EndDateTime = now.AddDays(21).Date.AddHours(13),
                Capacity = 18,
                Status = SessionStatus.Scheduled
            }
        );
        await context.SaveChangesAsync();
    }

    // ── Activity (enrolments, assessments, certs, payments, notifications) ────
    private static async Task SeedActivityAsync(AppDbContext context)
    {
        if (context.Enrollments.Any()) return;

        var ali = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0001");
        var layla = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0002");
        var omar = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0003");

        var james = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "instructor@platform.com");

        var sessions = await context.CourseSessions
            .Include(s => s.Course)
            .OrderBy(s => s.StartDateTime)
            .ToListAsync();

        var fundCompleted = sessions.First(s => s.Course.Title == "Networking Fundamentals" && s.Status == SessionStatus.Completed);
        var advCompleted = sessions.First(s => s.Course.Title == "Advanced Networking" && s.Status == SessionStatus.Completed);
        var cloudInProg = sessions.First(s => s.Course.Title == "Cloud Essentials" && s.Status == SessionStatus.InProgress);
        var fundScheduled = sessions.First(s => s.Course.Title == "Networking Fundamentals" && s.Status == SessionStatus.Scheduled);

        // Ali — completed both networking courses (eligible/issued cert)
        var aliFund = new Enrollment { TraineeId = ali.Id, CourseSessionId = fundCompleted.Id, Status = EnrollmentStatus.Completed, EnrolledAt = fundCompleted.StartDateTime.AddDays(-7) };
        var aliAdv = new Enrollment { TraineeId = ali.Id, CourseSessionId = advCompleted.Id, Status = EnrollmentStatus.Completed, EnrolledAt = advCompleted.StartDateTime.AddDays(-7) };
        // Layla — completed fundamentals only (still in progress on track)
        var laylaFund = new Enrollment { TraineeId = layla.Id, CourseSessionId = fundCompleted.Id, Status = EnrollmentStatus.Completed, EnrolledAt = fundCompleted.StartDateTime.AddDays(-7) };
        // Omar — currently attending cloud essentials
        var omarCloud = new Enrollment { TraineeId = omar.Id, CourseSessionId = cloudInProg.Id, Status = EnrollmentStatus.Attending, EnrolledAt = cloudInProg.StartDateTime.AddDays(-3) };
        // Layla — confirmed for upcoming fundamentals re-run (waitlist-style)
        var laylaUpcoming = new Enrollment { TraineeId = layla.Id, CourseSessionId = fundScheduled.Id, Status = EnrollmentStatus.Confirmed, EnrolledAt = DateTime.UtcNow.AddDays(-2) };

        context.Enrollments.AddRange(aliFund, aliAdv, laylaFund, omarCloud, laylaUpcoming);
        await context.SaveChangesAsync();

        // assessments — only for completed enrolments
        context.Assessments.AddRange(
            new Assessment
            {
                EnrollmentId = aliFund.Id,
                Result = AssessmentResult.Pass,
                Notes = "Strong grasp of subnetting and TCP/IP layering.",
                RecordedAt = fundCompleted.EndDateTime.AddHours(2),
                RecordedById = james.Id
            },
            new Assessment
            {
                EnrollmentId = aliAdv.Id,
                Result = AssessmentResult.Pass,
                Notes = "Excellent performance on routing and ACL labs.",
                RecordedAt = advCompleted.EndDateTime.AddHours(2),
                RecordedById = james.Id
            },
            new Assessment
            {
                EnrollmentId = laylaFund.Id,
                Result = AssessmentResult.Pass,
                Notes = "Solid fundamentals; ready to progress to advanced.",
                RecordedAt = fundCompleted.EndDateTime.AddHours(2),
                RecordedById = james.Id
            }
        );
        await context.SaveChangesAsync();

        // payments — Ali paid in full, Layla paid for both her enrolments, Omar partial
        context.Payments.AddRange(
            new Payment { EnrollmentId = aliFund.Id, AmountPaid = 150.00m, OutstandingBalance = 0, PaidAt = aliFund.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = aliAdv.Id, AmountPaid = 250.00m, OutstandingBalance = 0, PaidAt = aliAdv.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = laylaFund.Id, AmountPaid = 150.00m, OutstandingBalance = 0, PaidAt = laylaFund.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = laylaUpcoming.Id, AmountPaid = 0.00m, OutstandingBalance = 150, PaidAt = laylaUpcoming.EnrolledAt },
            new Payment { EnrollmentId = omarCloud.Id, AmountPaid = 100.00m, OutstandingBalance = 100, PaidAt = omarCloud.EnrolledAt.AddDays(1) }
        );
        await context.SaveChangesAsync();

        // certifications
        var netTrack = await context.CertificationTracks.FirstAsync(t => t.CertRefPrefix == "CERT-NET");
        var cloudTrack = await context.CertificationTracks.FirstAsync(t => t.CertRefPrefix == "CERT-CLD");

        context.TraineeCertifications.AddRange(
            new TraineeCertification
            {
                TraineeId = ali.Id,
                CertificationTrackId = netTrack.Id,
                Status = CertificationStatus.Issued,
                CertRefNumber = "CERT-NET-0001",
                IssuedAt = advCompleted.EndDateTime.AddDays(1)
            },
            new TraineeCertification
            {
                TraineeId = layla.Id,
                CertificationTrackId = netTrack.Id,
                Status = CertificationStatus.InProgress,
                CertRefNumber = string.Empty,
                IssuedAt = null
            },
            new TraineeCertification
            {
                TraineeId = omar.Id,
                CertificationTrackId = cloudTrack.Id,
                Status = CertificationStatus.InProgress,
                CertRefNumber = string.Empty,
                IssuedAt = null
            }
        );
        await context.SaveChangesAsync();

        // notifications
        var aliUser = await context.Users.FirstAsync(u => u.Email == "trainee@platform.com");
        var laylaUser = await context.Users.FirstAsync(u => u.Email == "layla@platform.com");
        var omarUser = await context.Users.FirstAsync(u => u.Email == "omar@platform.com");

        context.Notifications.AddRange(
            new Notification { UserId = aliUser.Id, Type = "Certificate", Message = "Your Certified Network Professional certificate has been issued. Ref: CERT-NET-0001.", CreatedAt = advCompleted.EndDateTime.AddDays(1), IsRead = false },
            new Notification { UserId = aliUser.Id, Type = "Welcome", Message = "Welcome back, Ali. New cloud courses are now available.", CreatedAt = DateTime.UtcNow.AddDays(-3), IsRead = true },
            new Notification { UserId = laylaUser.Id, Type = "Reminder", Message = "Your Networking Fundamentals re-run starts next week. Bring your laptop.", CreatedAt = DateTime.UtcNow.AddDays(-1), IsRead = false },
            new Notification { UserId = omarUser.Id, Type = "Payment", Message = "Outstanding balance of $100 remaining for Cloud Essentials.", CreatedAt = DateTime.UtcNow.AddHours(-12), IsRead = false }
        );
        await context.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private record AvailabilitySlot(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    private static async Task EnsureCoordinator(UserManager<AppUser> userManager,
        string email, string first, string last)
    {
        if (await userManager.FindByEmailAsync(email) != null) return;
        var user = new AppUser { UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Password1!");
        await userManager.AddToRoleAsync(user, "TrainingCoordinator");
    }

    private static async Task EnsureInstructor(UserManager<AppUser> userManager, AppDbContext context,
        string email, string first, string last, string bio, string expertise,
        AvailabilitySlot[] availability)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // Backfill ExpertiseAreas that was accidentally omitted in a prior seed run.
            var existing = await context.Instructors.FirstOrDefaultAsync(i => i.UserId == existingUser.Id);
            if (existing != null && string.IsNullOrEmpty(existing.ExpertiseAreas))
                existing.ExpertiseAreas = expertise;
            return;
        }

        var user = new AppUser { UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Password1!");
        await userManager.AddToRoleAsync(user, "Instructor");

        context.Instructors.Add(new Instructor
        {
            UserId = user.Id,
            Bio = bio,
            ExpertiseAreas = expertise,
            Availability = availability
                .Select(a => new InstructorAvailability { DayOfWeek = a.DayOfWeek, StartTime = a.StartTime, EndTime = a.EndTime })
                .ToList()
        });
    }

    private static async Task EnsureTrainee(UserManager<AppUser> userManager, AppDbContext context,
        string email, string first, string last, string publicId, string phone, DateOnly dob)
    {
        if (await userManager.FindByEmailAsync(email) != null) return;
        var user = new AppUser { UserName = email, Email = email, FirstName = first, LastName = last, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Password1!");
        await userManager.AddToRoleAsync(user, "Trainee");

        context.Trainees.Add(new Trainee
        {
            UserId = user.Id,
            TraineePublicId = publicId,
            Phone = phone,
            DateOfBirth = dob
        });
    }
}
