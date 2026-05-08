using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.API.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(
        AppDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
        {
            // runs migrations automatically if any are pending
            await context.Database.MigrateAsync();

            await SeedRolesAsync(roleManager);
            await SeedUsersAsync(userManager, context);
            await SeedCatalogAsync(context);
        }

        // ── Roles ─────────────────────────────────────────────────────────────────
        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = ["TrainingCoordinator", "Instructor", "Trainee"];

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // ── Users ─────────────────────────────────────────────────────────────────
        private static async Task SeedUsersAsync(
            UserManager<AppUser> userManager, AppDbContext context)
        {
            // coordinator
            if (await userManager.FindByEmailAsync("coordinator@platform.com") == null)
            {
                var coordinator = new AppUser
                {
                    UserName = "coordinator@platform.com",
                    Email = "coordinator@platform.com",
                    FirstName = "Sarah",
                    LastName = "Mitchell",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(coordinator, "Password1!");
                await userManager.AddToRoleAsync(coordinator, "TrainingCoordinator");
            }

            // instructor
            if (await userManager.FindByEmailAsync("instructor@platform.com") == null)
            {
                var instructorUser = new AppUser
                {
                    UserName = "instructor@platform.com",
                    Email = "instructor@platform.com",
                    FirstName = "James",
                    LastName = "Carter",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(instructorUser, "Password1!");
                await userManager.AddToRoleAsync(instructorUser, "Instructor");

                // create the instructor profile linked to this user
                var instructorProfile = new Instructor
                {
                    UserId = instructorUser.Id,
                    Bio = "Senior software engineer with 10 years of industry experience.",
                    Availability =
                    [
                        new InstructorAvailability
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0)
                    },
                    new InstructorAvailability
                    {
                        DayOfWeek = DayOfWeek.Wednesday,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0)
                    }
                    ]
                };
                context.Instructors.Add(instructorProfile);
            }

            // trainee
            if (await userManager.FindByEmailAsync("trainee@platform.com") == null)
            {
                var traineeUser = new AppUser
                {
                    UserName = "trainee@platform.com",
                    Email = "trainee@platform.com",
                    FirstName = "Ali",
                    LastName = "Hassan",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(traineeUser, "Password1!");
                await userManager.AddToRoleAsync(traineeUser, "Trainee");

                // create the trainee profile linked to this user
                var traineeProfile = new Trainee
                {
                    UserId = traineeUser.Id,
                    TraineePublicId = "TRN-0001",
                    Phone = "+97312345678",
                    DateOfBirth = new DateOnly(2000, 5, 15)
                };
                context.Trainees.Add(traineeProfile);
            }

            await context.SaveChangesAsync();
        }

        // ── Catalog ───────────────────────────────────────────────────────────────
        private static async Task SeedCatalogAsync(AppDbContext context)
        {
            // only seed if catalog is empty
            if (context.CourseCategories.Any()) return;

            // categories
            var networking = new CourseCategory
            {
                Name = "Networking",
                Description = "Network infrastructure and protocols"
            };
            var cloud = new CourseCategory
            {
                Name = "Cloud Computing",
                Description = "Cloud platforms and services"
            };
            context.CourseCategories.AddRange(networking, cloud);
            await context.SaveChangesAsync();

            // courses — networking fundamentals has no prerequisite
            // advanced networking requires fundamentals first
            var fundamentals = new Course
            {
                CategoryId = networking.Id,
                Title = "Networking Fundamentals",
                Description = "Core concepts of networking including TCP/IP and subnetting.",
                DurationHours = 20,
                Capacity = 20,
                EnrollmentFee = 150.00m
            };
            context.Courses.Add(fundamentals);
            await context.SaveChangesAsync();

            var advanced = new Course
            {
                CategoryId = networking.Id,
                PrerequisiteCourseId = fundamentals.Id, // requires fundamentals first
                Title = "Advanced Networking",
                Description = "Advanced routing, switching, and network security.",
                DurationHours = 30,
                Capacity = 15,
                EnrollmentFee = 250.00m
            };
            var cloudEssentials = new Course
            {
                CategoryId = cloud.Id,
                Title = "Cloud Essentials",
                Description = "Introduction to cloud computing concepts and AWS basics.",
                DurationHours = 25,
                Capacity = 20,
                EnrollmentFee = 200.00m
            };
            context.Courses.AddRange(advanced, cloudEssentials);
            await context.SaveChangesAsync();

            // classroom
            var classroom = new Classroom
            {
                Name = "Lab A",
                Capacity = 20,
                Equipment =
                [
                    new ClassroomEquipment { EquipmentName = "Projector" },
                new ClassroomEquipment { EquipmentName = "Lab Computers" }
                ]
            };
            context.Classrooms.Add(classroom);
            await context.SaveChangesAsync();

            // certification track
            var networkingTrack = new CertificationTrack
            {
                Name = "Certified Network Professional",
                Description = "Complete both networking courses to earn this certification.",
                CertRefPrefix = "CERT-NET"
            };
            context.CertificationTracks.Add(networkingTrack);
            await context.SaveChangesAsync();

            // link both networking courses to the track as required
            context.CertificationTrackCourses.AddRange(
                new CertificationTrackCourse
                {
                    CertificationTrackId = networkingTrack.Id,
                    CourseId = fundamentals.Id,
                    IsRequired = true
                },
                new CertificationTrackCourse
                {
                    CertificationTrackId = networkingTrack.Id,
                    CourseId = advanced.Id,
                    IsRequired = true
                }
            );

            // schedule a session — we need the instructor profile Id
            var instructor = context.Instructors.FirstOrDefault();
            if (instructor != null)
            {
                var session = new CourseSession
                {
                    CourseId = fundamentals.Id,
                    InstructorId = instructor.Id,
                    ClassroomId = classroom.Id,
                    StartDateTime = DateTime.UtcNow.AddDays(7),
                    EndDateTime = DateTime.UtcNow.AddDays(7).AddHours(4),
                    Capacity = 20,
                    Status = SessionStatus.Scheduled
                };
                context.CourseSessions.Add(session);
            }

            await context.SaveChangesAsync();
        }
    }
}