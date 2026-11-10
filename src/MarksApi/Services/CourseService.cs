using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Exceptions;
using MarksApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class CourseService : ICourseService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CourseService> _logger;

    public CourseService(AppDbContext context, ILogger<CourseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<CourseResponse>> GetAllAsync(PaginationParams pagination, CancellationToken ct = default)
    {
        var query = _context.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(search) ||
                                     c.Code.ToLower().Contains(search));
        }

        query = pagination.Sort?.ToLower() switch
        {
            "code_desc" => query.OrderByDescending(c => c.Code),
            "code" => query.OrderBy(c => c.Code),
            "title_desc" => query.OrderByDescending(c => c.Title),
            "title" => query.OrderBy(c => c.Title),
            "credits_desc" => query.OrderByDescending(c => c.CreditHours),
            "credits" => query.OrderBy(c => c.CreditHours),
            "id_desc" => query.OrderByDescending(c => c.Id),
            _ => query.OrderBy(c => c.Id)
        };

        var totalCount = await query.CountAsync(ct);

        var page = pagination.GetPage();
        var pageSize = pagination.GetPageSize();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseResponse
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(ct);

        return new PagedResult<CourseResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CourseResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponse
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (course == null)
        {
            throw new NotFoundException($"Course with ID {id} was not found.");
        }

        return course;
    }

    public async Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken ct = default)
    {
        ValidateCourseInput(request.Code, request.Title, request.CreditHours);

        var course = new Course
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            CreditHours = request.CreditHours,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Course created with ID {CourseId}, Code: {Code}", course.Id, course.Code);

        return new CourseResponse
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            CreditHours = course.CreditHours,
            CreatedAtUtc = course.CreatedAtUtc
        };
    }

    public async Task<CourseResponse> UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct = default)
    {
        ValidateCourseInput(request.Code, request.Title, request.CreditHours);

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (course == null)
        {
            throw new NotFoundException($"Course with ID {id} was not found.");
        }

        course.Code = request.Code.Trim().ToUpperInvariant();
        course.Title = request.Title.Trim();
        course.CreditHours = request.CreditHours;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Course updated with ID {CourseId}", course.Id);

        return new CourseResponse
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            CreditHours = course.CreditHours,
            CreatedAtUtc = course.CreatedAtUtc
        };
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var course = await _context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course == null)
        {
            throw new NotFoundException($"Course with ID {id} was not found.");
        }

        if (course.Enrollments.Any())
        {
            throw new ConflictException("Cannot delete course", "A course with existing enrollments cannot be deleted.");
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Course deleted with ID {CourseId}", id);
    }

    private static void ValidateCourseInput(string code, string title, int creditHours)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = new[] { "Course code is required." };
        }
        else if (code.Length > 10)
        {
            errors["code"] = new[] { "Course code cannot exceed 10 characters." };
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = new[] { "Course title is required." };
        }
        else if (title.Length > 150)
        {
            errors["title"] = new[] { "Course title cannot exceed 150 characters." };
        }

        if (creditHours < 1 || creditHours > 4)
        {
            errors["creditHours"] = new[] { "Credit hours must be between 1 and 4." };
        }

        if (errors.Any())
        {
            throw new ValidationException(errors);
        }
    }
}
