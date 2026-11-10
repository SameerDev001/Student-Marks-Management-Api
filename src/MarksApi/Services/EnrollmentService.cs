using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Exceptions;
using MarksApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(AppDbContext context, ILogger<EnrollmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<EnrollmentResponse>> GetByCourseIdAsync(int courseId, PaginationParams pagination, CancellationToken ct = default)
    {
        var courseExists = await _context.Courses.AnyAsync(c => c.Id == courseId, ct);
        if (!courseExists)
        {
            throw new NotFoundException($"Course with ID {courseId} was not found.");
        }

        var query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Where(e => e.CourseId == courseId);

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLower();
            query = query.Where(e => e.Student.FullName.ToLower().Contains(search) ||
                                     e.Student.RegistrationNo.ToLower().Contains(search));
        }

        query = pagination.Sort?.ToLower() switch
        {
            "mark_desc" => query.OrderByDescending(e => e.Mark),
            "mark" => query.OrderBy(e => e.Mark),
            "date_desc" => query.OrderByDescending(e => e.EnrolledAtUtc),
            "date" => query.OrderBy(e => e.EnrolledAtUtc),
            _ => query.OrderBy(e => e.Id)
        };

        var totalCount = await query.CountAsync(ct);

        var page = pagination.GetPage();
        var pageSize = pagination.GetPageSize();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EnrollmentResponse
            {
                Id = e.Id,
                StudentId = e.StudentId,
                CourseId = e.CourseId,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc,
                StudentName = e.Student.FullName,
                CourseCode = e.Course.Code
            })
            .ToListAsync(ct);

        return new PagedResult<EnrollmentResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<EnrollmentResponse> EnrollStudentAsync(int courseId, CreateEnrollmentRequest request, CancellationToken ct = default)
    {
        if (request.StudentId <= 0)
        {
            throw new ValidationException("studentId", "A valid student ID is required.");
        }

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null)
        {
            throw new NotFoundException($"Course with ID {courseId} was not found.");
        }

        var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId, ct);
        if (student == null)
        {
            throw new NotFoundException($"Student with ID {request.StudentId} was not found.");
        }

        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            Mark = null,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Enrolled student {StudentId} in course {CourseId}. Enrollment ID: {EnrollmentId}",
            student.Id, course.Id, enrollment.Id);

        return new EnrollmentResponse
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            Mark = enrollment.Mark,
            EnrolledAtUtc = enrollment.EnrolledAtUtc,
            StudentName = student.FullName,
            CourseCode = course.Code
        };
    }

    public async Task<EnrollmentResponse> UpdateMarkAsync(int id, UpdateMarkRequest request, CancellationToken ct = default)
    {
        if (request.Mark.HasValue && (request.Mark.Value < 0 || request.Mark.Value > 100))
        {
            throw new ValidationException("mark", "Mark must be a whole number between 0 and 100.");
        }

        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enrollment == null)
        {
            throw new NotFoundException($"Enrollment with ID {id} was not found.");
        }

        enrollment.Mark = request.Mark;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated mark for enrollment {EnrollmentId} to {Mark}", id, request.Mark);

        return new EnrollmentResponse
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            Mark = enrollment.Mark,
            EnrolledAtUtc = enrollment.EnrolledAtUtc,
            StudentName = enrollment.Student.FullName,
            CourseCode = enrollment.Course.Code
        };
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (enrollment == null)
        {
            throw new NotFoundException($"Enrollment with ID {id} was not found.");
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted enrollment with ID {EnrollmentId}", id);
    }
}
