using MarksApi.Configuration;
using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Exceptions;
using MarksApi.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MarksApi.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;
    private readonly MarkingSettings _markingSettings;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        AppDbContext context,
        IOptions<MarkingSettings> markingOptions,
        ILogger<ReportService> logger)
    {
        _context = context;
        _markingSettings = markingOptions.Value;
        _logger = logger;
    }

    public async Task<CourseStatsResponse> GetCourseStatsAsync(int courseId, CancellationToken ct = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, ct);

        if (course == null)
        {
            throw new NotFoundException($"Course with ID {courseId} was not found.");
        }

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .ToListAsync(ct);

        var enrolledCount = enrollments.Count;
        var gradedEnrollments = enrollments.Where(e => e.Mark.HasValue).ToList();
        var gradedCount = gradedEnrollments.Count;

        double? average = null;
        int? highest = null;
        int? lowest = null;
        var passedCount = 0;

        if (gradedCount > 0)
        {
            var marks = gradedEnrollments.Select(e => e.Mark!.Value).ToList();
            average = Math.Round(marks.Average(), 1);
            highest = marks.Max();
            lowest = marks.Min();
            passedCount = marks.Count(m => m >= _markingSettings.PassMark);
        }

        return new CourseStatsResponse
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            Enrolled = enrolledCount,
            Graded = gradedCount,
            Average = average,
            Highest = highest,
            Lowest = lowest,
            Passed = passedCount
        };
    }

    public async Task<TranscriptResponse> GetStudentTranscriptAsync(int studentId, int? currentStudentId, string currentRole, CancellationToken ct = default)
    {
        if (currentRole == Roles.Student && currentStudentId != studentId)
        {
            throw new ForbiddenException("Students may only view their own transcript.");
        }

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            throw new NotFoundException($"Student with ID {studentId} was not found.");
        }

        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .OrderBy(e => e.Course.Code)
            .ToListAsync(ct);

        var courseItems = new List<TranscriptCourseItem>();
        double totalWeightedMarks = 0;
        var totalGradedCredits = 0;

        foreach (var enrollment in enrollments)
        {
            var isPassed = enrollment.Mark.HasValue && enrollment.Mark.Value >= _markingSettings.PassMark;

            courseItems.Add(new TranscriptCourseItem
            {
                Code = enrollment.Course.Code,
                CreditHours = enrollment.Course.CreditHours,
                Mark = enrollment.Mark,
                Passed = isPassed
            });

            if (enrollment.Mark.HasValue)
            {
                totalWeightedMarks += enrollment.Mark.Value * enrollment.Course.CreditHours;
                totalGradedCredits += enrollment.Course.CreditHours;
            }
        }

        double? weightedAverage = null;
        if (totalGradedCredits > 0)
        {
            weightedAverage = Math.Round(totalWeightedMarks / totalGradedCredits, 1);
        }

        return new TranscriptResponse
        {
            StudentId = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Courses = courseItems,
            WeightedAverage = weightedAverage
        };
    }
}
