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

    // Gives each seeded course an image. Runs every startup but only fills in missing ones, so it won't overwrite images set in the app.
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

        await EnsureInstructor(userManager, context,
            email: "david@platform.com",
            first: "David", last: "Chen",
            bio: "Offensive security consultant and former incident responder.",
            expertise: "Cybersecurity, Penetration Testing, OWASP",
            availability:
            [
                new(DayOfWeek.Monday,  new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0))
            ]);

        await EnsureInstructor(userManager, context,
            email: "aisha@platform.com",
            first: "Aisha", last: "Noor",
            bio: "DevOps engineer focused on containers and delivery pipelines.",
            expertise: "DevOps, Docker, Kubernetes, CI/CD",
            availability:
            [
                new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new(DayOfWeek.Friday,    new TimeOnly(9, 0), new TimeOnly(17, 0))
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

        await EnsureTrainee(userManager, context,
            email: "fatima@platform.com", first: "Fatima", last: "Al-Sayed",
            publicId: "TRN-0004", phone: "+97312345681",
            dob: new DateOnly(1999, 7, 9));

        await EnsureTrainee(userManager, context,
            email: "yusuf@platform.com", first: "Yusuf", last: "Ibrahim",
            publicId: "TRN-0005", phone: "+97312345682",
            dob: new DateOnly(1997, 1, 30));

        await EnsureTrainee(userManager, context,
            email: "noura@platform.com", first: "Noura", last: "Al-Mansoori",
            publicId: "TRN-0006", phone: "+97312345683",
            dob: new DateOnly(2001, 3, 18));

        await EnsureTrainee(userManager, context,
            email: "khalid@platform.com", first: "Khalid", last: "Rahman",
            publicId: "TRN-0007", phone: "+97312345684",
            dob: new DateOnly(1996, 9, 5));

        await EnsureTrainee(userManager, context,
            email: "sara@platform.com", first: "Sara", last: "Haddad",
            publicId: "TRN-0008", phone: "+97312345685",
            dob: new DateOnly(2000, 12, 12));

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
        var devops = new CourseCategory { Name = "DevOps & Automation", Description = "Containers, pipelines, and delivery automation" };
        context.CourseCategories.AddRange(networking, cloud, security, devops);
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
        var azureFundamentals = new Course
        {
            CategoryId = cloud.Id,
            Title = "Azure Fundamentals",
            Description = "Core Azure services, identity, and governance for the AZ-900 path.",
            DurationHours = 20,
            Capacity = 20,
            EnrollmentFee = 190.00m
        };
        var ethicalHacking = new Course
        {
            CategoryId = security.Id,
            Title = "Ethical Hacking & Penetration Testing",
            Description = "Reconnaissance, exploitation, and reporting in authorized engagements.",
            DurationHours = 32,
            Capacity = 15,
            EnrollmentFee = 350.00m
        };
        var dockerK8s = new Course
        {
            CategoryId = devops.Id,
            Title = "Docker & Kubernetes",
            Description = "Containerize applications and orchestrate them at scale.",
            DurationHours = 28,
            Capacity = 18,
            EnrollmentFee = 300.00m
        };
        var cicd = new Course
        {
            CategoryId = devops.Id,
            Title = "CI/CD with GitHub Actions",
            Description = "Build automated test and deployment pipelines end to end.",
            DurationHours = 16,
            Capacity = 20,
            EnrollmentFee = 160.00m
        };
        context.Courses.AddRange(
            advanced, cloudEssentials, awsArchitect, secFundamentals,
            azureFundamentals, ethicalHacking, dockerK8s, cicd);
        await context.SaveChangesAsync();

        // wire prerequisites once dependency courses have Ids
        awsArchitect.PrerequisiteCourseId = cloudEssentials.Id;
        ethicalHacking.PrerequisiteCourseId = secFundamentals.Id;
        dockerK8s.PrerequisiteCourseId = cloudEssentials.Id;
        cicd.PrerequisiteCourseId = dockerK8s.Id;
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
        var labC = new Classroom
        {
            Name = "Lab C",
            Capacity = 25,
            Equipment =
            [
                new() { EquipmentName = "Projector" },
                new() { EquipmentName = "Lab Computers" },
                new() { EquipmentName = "Smart Board" }
            ]
        };
        var onlineRoom = new Classroom
        {
            Name = "Online Room",
            Capacity = 50,
            Equipment =
            [
                new() { EquipmentName = "Video Conferencing" },
                new() { EquipmentName = "Screen Sharing" }
            ]
        };
        context.Classrooms.AddRange(labA, labB, labC, onlineRoom);
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
            Description = "Pass the security fundamentals course, with ethical hacking as an elective.",
            CertRefPrefix = "CERT-SEC"
        };
        var devopsTrack = new CertificationTrack
        {
            Name = "Certified DevOps Engineer",
            Description = "Complete the containers and CI/CD pipeline courses.",
            CertRefPrefix = "CERT-DVO"
        };
        context.CertificationTracks.AddRange(netTrack, cloudTrack, secTrack, devopsTrack);
        await context.SaveChangesAsync();

        context.CertificationTrackCourses.AddRange(
            new() { CertificationTrackId = netTrack.Id, CourseId = fundamentals.Id, IsRequired = true },
            new() { CertificationTrackId = netTrack.Id, CourseId = advanced.Id, IsRequired = true },
            new() { CertificationTrackId = cloudTrack.Id, CourseId = cloudEssentials.Id, IsRequired = true },
            new() { CertificationTrackId = cloudTrack.Id, CourseId = awsArchitect.Id, IsRequired = true },
            new() { CertificationTrackId = cloudTrack.Id, CourseId = azureFundamentals.Id, IsRequired = false },
            new() { CertificationTrackId = secTrack.Id, CourseId = secFundamentals.Id, IsRequired = true },
            new() { CertificationTrackId = secTrack.Id, CourseId = ethicalHacking.Id, IsRequired = false },
            new() { CertificationTrackId = devopsTrack.Id, CourseId = dockerK8s.Id, IsRequired = true },
            new() { CertificationTrackId = devopsTrack.Id, CourseId = cicd.Id, IsRequired = true }
        );
        await context.SaveChangesAsync();

        // sessions — a fresh mix of past, in-progress, and upcoming offerings.
        // Every course gets at least one upcoming Scheduled session so trainees
        // can always reach it from the course catalog's "Find Sessions" button.
        var james = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "instructor@platform.com");
        var maria = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "maria@platform.com");
        var david = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "david@platform.com");
        var aisha = await context.Instructors
            .Include(i => i.User)
            .FirstAsync(i => i.User.Email == "aisha@platform.com");

        var now = DateTime.UtcNow;

        // Builds a single-day session running `hours` from `hour:00` on the given day offset.
        CourseSession Session(Course course, Instructor inst, Classroom room,
            int dayOffset, int hour, int hours, int capacity, SessionStatus status)
        {
            var start = now.AddDays(dayOffset).Date.AddHours(hour);
            return new CourseSession
            {
                CourseId = course.Id,
                InstructorId = inst.Id,
                ClassroomId = room.Id,
                StartDateTime = start,
                EndDateTime = start.AddHours(hours),
                Capacity = capacity,
                Status = status
            };
        }

        context.CourseSessions.AddRange(
            // — completed (history for reports, assessments, certificates) —
            Session(fundamentals,   james, labA, -45, 9,  4, 20, SessionStatus.Completed),
            Session(advanced,       james, labA, -20, 9,  4, 15, SessionStatus.Completed),
            Session(secFundamentals, david, labB, -30, 9,  4, 15, SessionStatus.Completed),
            Session(cloudEssentials, maria, labC, -35, 10, 4, 22, SessionStatus.Completed),

            // — in progress (start just behind us, end ahead) —
            new CourseSession
            {
                CourseId = cloudEssentials.Id, InstructorId = maria.Id, ClassroomId = onlineRoom.Id,
                StartDateTime = now.AddHours(-2), EndDateTime = now.AddHours(4),
                Capacity = 30, Status = SessionStatus.InProgress
            },
            new CourseSession
            {
                CourseId = dockerK8s.Id, InstructorId = aisha.Id, ClassroomId = labC.Id,
                StartDateTime = now.AddHours(-1), EndDateTime = now.AddHours(5),
                Capacity = 18, Status = SessionStatus.InProgress
            },

            // — scheduled / upcoming (one or more per course) —
            Session(fundamentals,        james, labA,  7,  9,  4, 20, SessionStatus.Scheduled),
            Session(fundamentals,        aisha, labC,  28, 13, 4, 25, SessionStatus.Scheduled),
            Session(advanced,            james, labA,  18, 9,  5, 15, SessionStatus.Scheduled),
            Session(cloudEssentials,     maria, labB,  10, 10, 4, 15, SessionStatus.Scheduled),
            Session(awsArchitect,        maria, onlineRoom, 14, 10, 5, 30, SessionStatus.Scheduled),
            Session(azureFundamentals,   maria, labB,  30, 10, 4, 15, SessionStatus.Scheduled),
            Session(secFundamentals,     david, labB,  21, 9,  4, 15, SessionStatus.Scheduled),
            Session(ethicalHacking,      david, labC,  35, 9,  5, 15, SessionStatus.Scheduled),
            Session(dockerK8s,           aisha, labC,  25, 9,  5, 18, SessionStatus.Scheduled),
            Session(cicd,                aisha, onlineRoom, 12, 13, 4, 20, SessionStatus.Scheduled)
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
        var fatima = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0004");
        var yusuf = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0005");
        var noura = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0006");
        var khalid = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0007");
        var sara = await context.Trainees.FirstAsync(t => t.TraineePublicId == "TRN-0008");

        var james = await context.Instructors.Include(i => i.User).FirstAsync(i => i.User.Email == "instructor@platform.com");
        var maria = await context.Instructors.Include(i => i.User).FirstAsync(i => i.User.Email == "maria@platform.com");
        var david = await context.Instructors.Include(i => i.User).FirstAsync(i => i.User.Email == "david@platform.com");

        var sessions = await context.CourseSessions
            .Include(s => s.Course)
            .OrderBy(s => s.StartDateTime)
            .ToListAsync();

        CourseSession Find(string courseTitle, SessionStatus status) =>
            sessions.First(s => s.Course.Title == courseTitle && s.Status == status);

        var fundCompleted  = Find("Networking Fundamentals", SessionStatus.Completed);
        var advCompleted   = Find("Advanced Networking", SessionStatus.Completed);
        var secCompleted   = Find("Security Fundamentals", SessionStatus.Completed);
        var cloudCompleted = Find("Cloud Essentials", SessionStatus.Completed);
        var cloudInProg    = Find("Cloud Essentials", SessionStatus.InProgress);
        var dockerInProg   = Find("Docker & Kubernetes", SessionStatus.InProgress);
        var fundScheduled  = Find("Networking Fundamentals", SessionStatus.Scheduled);
        var advScheduled   = Find("Advanced Networking", SessionStatus.Scheduled);
        var secScheduled   = Find("Security Fundamentals", SessionStatus.Scheduled);
        var awsScheduled   = Find("AWS Solutions Architect", SessionStatus.Scheduled);
        var ethicalSched   = Find("Ethical Hacking & Penetration Testing", SessionStatus.Scheduled);
        var cicdScheduled  = Find("CI/CD with GitHub Actions", SessionStatus.Scheduled);

        Enrollment Enrol(Trainee t, CourseSession s, EnrollmentStatus status, int enrolledDaysBeforeStart) =>
            new() { TraineeId = t.Id, CourseSessionId = s.Id, Status = status, EnrolledAt = s.StartDateTime.AddDays(-enrolledDaysBeforeStart) };

        // Ali — finished both networking courses (issued certificate)
        var aliFund = Enrol(ali, fundCompleted, EnrollmentStatus.Completed, 7);
        var aliAdv  = Enrol(ali, advCompleted, EnrollmentStatus.Completed, 7);
        // Layla — finished fundamentals, confirmed for advanced (track in progress)
        var laylaFund = Enrol(layla, fundCompleted, EnrollmentStatus.Completed, 7);
        var laylaAdv  = Enrol(layla, advScheduled, EnrollmentStatus.Confirmed, 5);
        // Omar — currently attending cloud essentials
        var omarCloud = Enrol(omar, cloudInProg, EnrollmentStatus.Attending, 3);
        // Fatima — passed security fundamentals (issued), signed up for ethical hacking
        var fatimaSec     = Enrol(fatima, secCompleted, EnrollmentStatus.Completed, 7);
        var fatimaEthical = Enrol(fatima, ethicalSched, EnrollmentStatus.Enrolled, 4);
        // Yusuf — attending docker, confirmed for CI/CD (devops track in progress)
        var yusufDocker = Enrol(yusuf, dockerInProg, EnrollmentStatus.Attending, 2);
        var yusufCicd   = Enrol(yusuf, cicdScheduled, EnrollmentStatus.Confirmed, 6);
        // Noura — finished cloud essentials, confirmed for AWS architect
        var nouraCloud = Enrol(noura, cloudCompleted, EnrollmentStatus.Completed, 7);
        var nouraAws   = Enrol(noura, awsScheduled, EnrollmentStatus.Confirmed, 5);
        // Khalid — newly enrolled in the upcoming fundamentals run
        var khalidFund = Enrol(khalid, fundScheduled, EnrollmentStatus.Enrolled, 3);
        // Sara — failed security fundamentals, confirmed for a retake
        var saraSecFailed  = Enrol(sara, secCompleted, EnrollmentStatus.Completed, 7);
        var saraSecRetake  = Enrol(sara, secScheduled, EnrollmentStatus.Confirmed, 4);

        context.Enrollments.AddRange(
            aliFund, aliAdv, laylaFund, laylaAdv, omarCloud,
            fatimaSec, fatimaEthical, yusufDocker, yusufCicd,
            nouraCloud, nouraAws, khalidFund, saraSecFailed, saraSecRetake);
        await context.SaveChangesAsync();

        // assessments — only for completed enrolments
        context.Assessments.AddRange(
            new Assessment { EnrollmentId = aliFund.Id, Result = AssessmentResult.Pass, Notes = "Strong grasp of subnetting and TCP/IP layering.", RecordedAt = fundCompleted.EndDateTime.AddHours(2), RecordedById = james.Id },
            new Assessment { EnrollmentId = aliAdv.Id, Result = AssessmentResult.Pass, Notes = "Excellent performance on routing and ACL labs.", RecordedAt = advCompleted.EndDateTime.AddHours(2), RecordedById = james.Id },
            new Assessment { EnrollmentId = laylaFund.Id, Result = AssessmentResult.Pass, Notes = "Solid fundamentals; ready to progress to advanced.", RecordedAt = fundCompleted.EndDateTime.AddHours(2), RecordedById = james.Id },
            new Assessment { EnrollmentId = fatimaSec.Id, Result = AssessmentResult.Pass, Notes = "Confident with OWASP top 10 and threat modelling.", RecordedAt = secCompleted.EndDateTime.AddHours(2), RecordedById = david.Id },
            new Assessment { EnrollmentId = nouraCloud.Id, Result = AssessmentResult.Pass, Notes = "Good command of core cloud concepts and AWS basics.", RecordedAt = cloudCompleted.EndDateTime.AddHours(2), RecordedById = maria.Id },
            new Assessment { EnrollmentId = saraSecFailed.Id, Result = AssessmentResult.Fail, Notes = "Struggled with the defensive coding assessment; retake recommended.", RecordedAt = secCompleted.EndDateTime.AddHours(2), RecordedById = david.Id }
        );
        await context.SaveChangesAsync();

        // payments — full for completed/paid, partial or outstanding for in-progress and upcoming
        context.Payments.AddRange(
            new Payment { EnrollmentId = aliFund.Id, AmountPaid = 150.00m, OutstandingBalance = 0, PaidAt = aliFund.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = aliAdv.Id, AmountPaid = 250.00m, OutstandingBalance = 0, PaidAt = aliAdv.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = laylaFund.Id, AmountPaid = 150.00m, OutstandingBalance = 0, PaidAt = laylaFund.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = laylaAdv.Id, AmountPaid = 0.00m, OutstandingBalance = 250, PaidAt = laylaAdv.EnrolledAt },
            new Payment { EnrollmentId = omarCloud.Id, AmountPaid = 100.00m, OutstandingBalance = 100, PaidAt = omarCloud.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = fatimaSec.Id, AmountPaid = 180.00m, OutstandingBalance = 0, PaidAt = fatimaSec.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = fatimaEthical.Id, AmountPaid = 0.00m, OutstandingBalance = 350, PaidAt = fatimaEthical.EnrolledAt },
            new Payment { EnrollmentId = yusufDocker.Id, AmountPaid = 150.00m, OutstandingBalance = 150, PaidAt = yusufDocker.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = yusufCicd.Id, AmountPaid = 0.00m, OutstandingBalance = 160, PaidAt = yusufCicd.EnrolledAt },
            new Payment { EnrollmentId = nouraCloud.Id, AmountPaid = 200.00m, OutstandingBalance = 0, PaidAt = nouraCloud.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = nouraAws.Id, AmountPaid = 200.00m, OutstandingBalance = 200, PaidAt = nouraAws.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = khalidFund.Id, AmountPaid = 0.00m, OutstandingBalance = 150, PaidAt = khalidFund.EnrolledAt },
            new Payment { EnrollmentId = saraSecFailed.Id, AmountPaid = 180.00m, OutstandingBalance = 0, PaidAt = saraSecFailed.EnrolledAt.AddDays(1) },
            new Payment { EnrollmentId = saraSecRetake.Id, AmountPaid = 0.00m, OutstandingBalance = 180, PaidAt = saraSecRetake.EnrolledAt }
        );
        await context.SaveChangesAsync();

        // certifications
        var netTrack = await context.CertificationTracks.FirstAsync(t => t.CertRefPrefix == "CERT-NET");
        var cloudTrack = await context.CertificationTracks.FirstAsync(t => t.CertRefPrefix == "CERT-CLD");
        var secTrack = await context.CertificationTracks.FirstAsync(t => t.CertRefPrefix == "CERT-SEC");
        var devopsTrack = await context.CertificationTracks.FirstAsync(t => t.CertRefPrefix == "CERT-DVO");

        context.TraineeCertifications.AddRange(
            new TraineeCertification { TraineeId = ali.Id, CertificationTrackId = netTrack.Id, Status = CertificationStatus.Issued, CertRefNumber = "CERT-NET-0001", IssuedAt = advCompleted.EndDateTime.AddDays(1) },
            new TraineeCertification { TraineeId = layla.Id, CertificationTrackId = netTrack.Id, Status = CertificationStatus.InProgress, CertRefNumber = string.Empty, IssuedAt = null },
            new TraineeCertification { TraineeId = omar.Id, CertificationTrackId = cloudTrack.Id, Status = CertificationStatus.InProgress, CertRefNumber = string.Empty, IssuedAt = null },
            new TraineeCertification { TraineeId = fatima.Id, CertificationTrackId = secTrack.Id, Status = CertificationStatus.Issued, CertRefNumber = "CERT-SEC-0001", IssuedAt = secCompleted.EndDateTime.AddDays(1) },
            new TraineeCertification { TraineeId = noura.Id, CertificationTrackId = cloudTrack.Id, Status = CertificationStatus.InProgress, CertRefNumber = string.Empty, IssuedAt = null },
            new TraineeCertification { TraineeId = yusuf.Id, CertificationTrackId = devopsTrack.Id, Status = CertificationStatus.InProgress, CertRefNumber = string.Empty, IssuedAt = null }
        );
        await context.SaveChangesAsync();

        // notifications
        var aliUser = await context.Users.FirstAsync(u => u.Email == "trainee@platform.com");
        var laylaUser = await context.Users.FirstAsync(u => u.Email == "layla@platform.com");
        var omarUser = await context.Users.FirstAsync(u => u.Email == "omar@platform.com");
        var fatimaUser = await context.Users.FirstAsync(u => u.Email == "fatima@platform.com");
        var yusufUser = await context.Users.FirstAsync(u => u.Email == "yusuf@platform.com");
        var khalidUser = await context.Users.FirstAsync(u => u.Email == "khalid@platform.com");
        var saraUser = await context.Users.FirstAsync(u => u.Email == "sara@platform.com");

        context.Notifications.AddRange(
            new Notification { UserId = aliUser.Id, Type = "Certificate", Message = "Your Certified Network Professional certificate has been issued. Ref: CERT-NET-0001.", CreatedAt = advCompleted.EndDateTime.AddDays(1), IsRead = false },
            new Notification { UserId = aliUser.Id, Type = "Welcome", Message = "Welcome back, Ali. New cloud courses are now available.", CreatedAt = DateTime.UtcNow.AddDays(-3), IsRead = true },
            new Notification { UserId = laylaUser.Id, Type = "Reminder", Message = "Your Advanced Networking session is confirmed. Bring your laptop.", CreatedAt = DateTime.UtcNow.AddDays(-1), IsRead = false },
            new Notification { UserId = laylaUser.Id, Type = "Payment", Message = "Outstanding balance of $250 remaining for Advanced Networking.", CreatedAt = DateTime.UtcNow.AddDays(-1), IsRead = false },
            new Notification { UserId = omarUser.Id, Type = "Payment", Message = "Outstanding balance of $100 remaining for Cloud Essentials.", CreatedAt = DateTime.UtcNow.AddHours(-12), IsRead = false },
            new Notification { UserId = fatimaUser.Id, Type = "Certificate", Message = "Your Certified Security Associate certificate has been issued. Ref: CERT-SEC-0001.", CreatedAt = secCompleted.EndDateTime.AddDays(1), IsRead = false },
            new Notification { UserId = yusufUser.Id, Type = "Reminder", Message = "Your Docker & Kubernetes session is underway. See you in the lab.", CreatedAt = DateTime.UtcNow.AddHours(-6), IsRead = false },
            new Notification { UserId = khalidUser.Id, Type = "Welcome", Message = "You're enrolled in Networking Fundamentals. Complete payment to confirm your seat.", CreatedAt = DateTime.UtcNow.AddHours(-20), IsRead = false },
            new Notification { UserId = saraUser.Id, Type = "Assessment", Message = "Security Fundamentals result recorded. A retake has been scheduled for you.", CreatedAt = secCompleted.EndDateTime.AddHours(3), IsRead = false }
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
