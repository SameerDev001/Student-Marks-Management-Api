using MarksApi.Authentication;
using MarksApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, IPasswordService passwordService)
    {
        if (await context.Users.AnyAsync() || await context.Students.AnyAsync())
        {
            return;
        }

        var utcNow = DateTime.UtcNow;

        // 1. Students
        var student1 = new Student
        {
            RegistrationNo = "CS-2022-001",
            FullName = "Ali Raza",
            Email = "ali.raza@example.com",
            CreatedAtUtc = utcNow
        };

        var student2 = new Student
        {
            RegistrationNo = "CS-2022-002",
            FullName = "Sara Khan",
            Email = "sara.khan@example.com",
            CreatedAtUtc = utcNow
        };

        context.Students.AddRange(student1, student2);
        await context.SaveChangesAsync();

        // 2. Users
        var teacherUser = new User
        {
            Username = "teacher1",
            PasswordHash = passwordService.HashPassword("TeacherPassword123!"),
            Role = Roles.Teacher,
            StudentId = null
        };

        var studentUser1 = new User
        {
            Username = "student1",
            PasswordHash = passwordService.HashPassword("StudentPassword123!"),
            Role = Roles.Student,
            StudentId = student1.Id
        };

        var studentUser2 = new User
        {
            Username = "student2",
            PasswordHash = passwordService.HashPassword("StudentPassword123!"),
            Role = Roles.Student,
            StudentId = student2.Id
        };

        context.Users.AddRange(teacherUser, studentUser1, studentUser2);
        await context.SaveChangesAsync();

        // 3. Courses
        var course1 = new Course
        {
            Code = "CS401",
            Title = "Web Application Development",
            CreditHours = 3,
            CreatedAtUtc = utcNow
        };

        var course2 = new Course
        {
            Code = "MA201",
            Title = "Linear Algebra",
            CreditHours = 2,
            CreatedAtUtc = utcNow
        };

        var course3 = new Course
        {
            Code = "CS302",
            Title = "Data Structures and Algorithms",
            CreditHours = 4,
            CreatedAtUtc = utcNow
        };

        context.Courses.AddRange(course1, course2, course3);
        await context.SaveChangesAsync();

        // 4. Enrollments (6 enrollments)
        var enrollments = new List<Enrollment>
        {
            new() { StudentId = student1.Id, CourseId = course1.Id, Mark = 80, EnrolledAtUtc = utcNow },
            new() { StudentId = student1.Id, CourseId = course2.Id, Mark = 60, EnrolledAtUtc = utcNow },
            new() { StudentId = student1.Id, CourseId = course3.Id, Mark = null, EnrolledAtUtc = utcNow },

            new() { StudentId = student2.Id, CourseId = course1.Id, Mark = 70, EnrolledAtUtc = utcNow },
            new() { StudentId = student2.Id, CourseId = course2.Id, Mark = 45, EnrolledAtUtc = utcNow },
            new() { StudentId = student2.Id, CourseId = course3.Id, Mark = 90, EnrolledAtUtc = utcNow }
        };

        context.Enrollments.AddRange(enrollments);
        await context.SaveChangesAsync();
    }
}
