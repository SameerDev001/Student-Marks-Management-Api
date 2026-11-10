using MarksApi.Configuration;
using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Exceptions;
using MarksApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MarksApi.Tests;

public class ServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ReportService_WeightedAverage_CalculatesCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var student = new Student { Id = 1, RegistrationNo = "CS-001", FullName = "Ali Raza", Email = "ali@example.com", CreatedAtUtc = DateTime.UtcNow };
        var course1 = new Course { Id = 1, Code = "CS401", Title = "Web Dev", CreditHours = 3, CreatedAtUtc = DateTime.UtcNow };
        var course2 = new Course { Id = 2, Code = "MA201", Title = "Linear Algebra", CreditHours = 2, CreatedAtUtc = DateTime.UtcNow };

        context.Students.Add(student);
        context.Courses.AddRange(course1, course2);
        context.Enrollments.AddRange(
            new Enrollment { Id = 1, StudentId = 1, CourseId = 1, Mark = 80, EnrolledAtUtc = DateTime.UtcNow, Course = course1, Student = student },
            new Enrollment { Id = 2, StudentId = 1, CourseId = 2, Mark = 60, EnrolledAtUtc = DateTime.UtcNow, Course = course2, Student = student }
        );
        await context.SaveChangesAsync();

        var markingOptions = Options.Create(new MarkingSettings { PassMark = 50 });
        var reportService = new ReportService(context, markingOptions, NullLogger<ReportService>.Instance);

        // Act
        var transcript = await reportService.GetStudentTranscriptAsync(1, 1, Roles.Student);

        // Assert: (80*3 + 60*2) / (3+2) = 360 / 5 = 72.0
        Assert.NotNull(transcript);
        Assert.Equal(72.0, transcript.WeightedAverage);
        Assert.Equal(2, transcript.Courses.Count);
        Assert.True(transcript.Courses[0].Passed);
        Assert.True(transcript.Courses[1].Passed);
    }

    [Fact]
    public async Task ReportService_UngradedCourses_ExcludedFromWeightedAverage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var student = new Student { Id = 1, RegistrationNo = "CS-001", FullName = "Ali Raza", Email = "ali@example.com", CreatedAtUtc = DateTime.UtcNow };
        var course1 = new Course { Id = 1, Code = "CS401", Title = "Web Dev", CreditHours = 3, CreatedAtUtc = DateTime.UtcNow };
        var course2 = new Course { Id = 2, Code = "CS302", Title = "DSA", CreditHours = 4, CreatedAtUtc = DateTime.UtcNow };

        context.Students.Add(student);
        context.Courses.AddRange(course1, course2);
        context.Enrollments.AddRange(
            new Enrollment { Id = 1, StudentId = 1, CourseId = 1, Mark = 90, EnrolledAtUtc = DateTime.UtcNow, Course = course1, Student = student },
            new Enrollment { Id = 2, StudentId = 1, CourseId = 2, Mark = null, EnrolledAtUtc = DateTime.UtcNow, Course = course2, Student = student }
        );
        await context.SaveChangesAsync();

        var reportService = new ReportService(context, Options.Create(new MarkingSettings { PassMark = 50 }), NullLogger<ReportService>.Instance);

        // Act
        var transcript = await reportService.GetStudentTranscriptAsync(1, null, Roles.Teacher);

        // Assert: only graded course (mark 90, 3 cr) counted -> average 90.0
        Assert.NotNull(transcript);
        Assert.Equal(90.0, transcript.WeightedAverage);
        Assert.Null(transcript.Courses.First(c => c.Code == "CS302").Mark);
        Assert.False(transcript.Courses.First(c => c.Code == "CS302").Passed);
    }

    [Fact]
    public async Task ReportService_ZeroGradedCourses_ReturnsNullAverage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var student = new Student { Id = 1, RegistrationNo = "CS-001", FullName = "Ali Raza", Email = "ali@example.com", CreatedAtUtc = DateTime.UtcNow };
        var course = new Course { Id = 1, Code = "CS401", Title = "Web Dev", CreditHours = 3, CreatedAtUtc = DateTime.UtcNow };

        context.Students.Add(student);
        context.Courses.Add(course);
        context.Enrollments.Add(new Enrollment { Id = 1, StudentId = 1, CourseId = 1, Mark = null, EnrolledAtUtc = DateTime.UtcNow, Course = course, Student = student });
        await context.SaveChangesAsync();

        var reportService = new ReportService(context, Options.Create(new MarkingSettings { PassMark = 50 }), NullLogger<ReportService>.Instance);

        // Act
        var transcript = await reportService.GetStudentTranscriptAsync(1, null, Roles.Teacher);

        // Assert: When no course is graded, weighted average MUST be null
        Assert.NotNull(transcript);
        Assert.Null(transcript.WeightedAverage);
    }

    [Fact]
    public async Task ReportService_CourseStats_CalculatesPassedCountAndAverage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var course = new Course { Id = 1, Code = "CS401", Title = "Web Dev", CreditHours = 3, CreatedAtUtc = DateTime.UtcNow };
        context.Courses.Add(course);
        context.Enrollments.AddRange(
            new Enrollment { Id = 1, StudentId = 1, CourseId = 1, Mark = 80, EnrolledAtUtc = DateTime.UtcNow },
            new Enrollment { Id = 2, StudentId = 2, CourseId = 1, Mark = 70, EnrolledAtUtc = DateTime.UtcNow },
            new Enrollment { Id = 3, StudentId = 3, CourseId = 1, Mark = null, EnrolledAtUtc = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var reportService = new ReportService(context, Options.Create(new MarkingSettings { PassMark = 50 }), NullLogger<ReportService>.Instance);

        // Act
        var stats = await reportService.GetCourseStatsAsync(1);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(3, stats.Enrolled);
        Assert.Equal(2, stats.Graded);
        Assert.Equal(75.0, stats.Average);
        Assert.Equal(80, stats.Highest);
        Assert.Equal(70, stats.Lowest);
        Assert.Equal(2, stats.Passed);
    }

    [Fact]
    public async Task ReportService_CourseStats_NoGradedStudents_ReturnsNullAverage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var course = new Course { Id = 1, Code = "CS401", Title = "Web Dev", CreditHours = 3, CreatedAtUtc = DateTime.UtcNow };
        context.Courses.Add(course);
        context.Enrollments.Add(new Enrollment { Id = 1, StudentId = 1, CourseId = 1, Mark = null, EnrolledAtUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var reportService = new ReportService(context, Options.Create(new MarkingSettings { PassMark = 50 }), NullLogger<ReportService>.Instance);

        // Act
        var stats = await reportService.GetCourseStatsAsync(1);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Enrolled);
        Assert.Equal(0, stats.Graded);
        Assert.Null(stats.Average);
        Assert.Null(stats.Highest);
        Assert.Null(stats.Lowest);
        Assert.Equal(0, stats.Passed);
    }

    [Fact]
    public async Task StudentService_Validation_ThrowsOnEmptyRegistrationNo()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var studentService = new StudentService(context, NullLogger<StudentService>.Instance);

        var request = new CreateStudentRequest
        {
            RegistrationNo = "",
            FullName = "Sameer",
            Email = "sameer@example.com"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => studentService.CreateAsync(request));
        Assert.True(ex.Errors?.ContainsKey("registrationNo"));
    }

    [Fact]
    public async Task StudentService_Validation_ThrowsOnInvalidEmail()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var studentService = new StudentService(context, NullLogger<StudentService>.Instance);

        var request = new CreateStudentRequest
        {
            RegistrationNo = "CS-005",
            FullName = "Test Student",
            Email = "not-an-email"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => studentService.CreateAsync(request));
        Assert.True(ex.Errors?.ContainsKey("email"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task CourseService_Validation_ThrowsWhenCreditHoursOutsideRange(int invalidCredits)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var courseService = new CourseService(context, NullLogger<CourseService>.Instance);

        var request = new CreateCourseRequest
        {
            Code = "CS101",
            Title = "Intro to CS",
            CreditHours = invalidCredits
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => courseService.CreateAsync(request));
        Assert.True(ex.Errors?.ContainsKey("creditHours"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task EnrollmentService_UpdateMark_ThrowsWhenMarkOutsideRange(int invalidMark)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var enrollmentService = new EnrollmentService(context, NullLogger<EnrollmentService>.Instance);

        var request = new UpdateMarkRequest { Mark = invalidMark };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => enrollmentService.UpdateMarkAsync(1, request));
        Assert.True(ex.Errors?.ContainsKey("mark"));
    }

    [Fact]
    public async Task StudentService_Ownership_ThrowsForbiddenWhenAccessingAnotherStudent()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var studentService = new StudentService(context, NullLogger<StudentService>.Instance);

        // Student with ID 1 trying to access Student with ID 2
        var requestingStudentId = 1;
        var targetStudentId = 2;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            studentService.GetByIdAsync(targetStudentId, requestingStudentId, Roles.Student));

        Assert.Equal(403, ex.StatusCode);
    }
}
