using System.Text.RegularExpressions;
using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Exceptions;
using MarksApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class StudentService : IStudentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<StudentService> _logger;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public StudentService(AppDbContext context, ILogger<StudentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<StudentResponse>> GetAllAsync(PaginationParams pagination, CancellationToken ct = default)
    {
        var query = _context.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLower();
            query = query.Where(s => s.FullName.ToLower().Contains(search) ||
                                     s.RegistrationNo.ToLower().Contains(search) ||
                                     s.Email.ToLower().Contains(search));
        }

        query = pagination.Sort?.ToLower() switch
        {
            "name_desc" => query.OrderByDescending(s => s.FullName),
            "name" => query.OrderBy(s => s.FullName),
            "reg_desc" => query.OrderByDescending(s => s.RegistrationNo),
            "reg" => query.OrderBy(s => s.RegistrationNo),
            "id_desc" => query.OrderByDescending(s => s.Id),
            _ => query.OrderBy(s => s.Id)
        };

        var totalCount = await query.CountAsync(ct);

        var page = pagination.GetPage();
        var pageSize = pagination.GetPageSize();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(ct);

        return new PagedResult<StudentResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<StudentResponse> GetByIdAsync(int id, int? currentStudentId, string currentRole, CancellationToken ct = default)
    {
        if (currentRole == Roles.Student && currentStudentId != id)
        {
            throw new ForbiddenException("Students may only view their own student record.");
        }

        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (student == null)
        {
            throw new NotFoundException($"Student with ID {id} was not found.");
        }

        return student;
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        ValidateStudentInput(request.RegistrationNo, request.FullName, request.Email);

        var student = new Student
        {
            RegistrationNo = request.RegistrationNo.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Student created with ID {StudentId}, RegNo: {RegNo}", student.Id, student.RegistrationNo);

        return new StudentResponse
        {
            Id = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Email = student.Email,
            CreatedAtUtc = student.CreatedAtUtc
        };
    }

    public async Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        ValidateStudentInput(request.RegistrationNo, request.FullName, request.Email);

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
        {
            throw new NotFoundException($"Student with ID {id} was not found.");
        }

        student.RegistrationNo = request.RegistrationNo.Trim();
        student.FullName = request.FullName.Trim();
        student.Email = request.Email.Trim().ToLowerInvariant();

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Student updated with ID {StudentId}", student.Id);

        return new StudentResponse
        {
            Id = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Email = student.Email,
            CreatedAtUtc = student.CreatedAtUtc
        };
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var student = await _context.Students
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (student == null)
        {
            throw new NotFoundException($"Student with ID {id} was not found.");
        }

        if (student.Enrollments.Any())
        {
            throw new ConflictException("Cannot delete student", "A student with existing enrollments cannot be deleted.");
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Student deleted with ID {StudentId}", id);
    }

    private static void ValidateStudentInput(string registrationNo, string fullName, string email)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(registrationNo))
        {
            errors["registrationNo"] = new[] { "Registration number is required." };
        }
        else if (registrationNo.Length > 20)
        {
            errors["registrationNo"] = new[] { "Registration number cannot exceed 20 characters." };
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["fullName"] = new[] { "Full name is required." };
        }
        else if (fullName.Length > 100)
        {
            errors["fullName"] = new[] { "Full name cannot exceed 100 characters." };
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = new[] { "Email is required." };
        }
        else if (email.Length > 150)
        {
            errors["email"] = new[] { "Email cannot exceed 150 characters." };
        }
        else if (!EmailRegex.IsMatch(email))
        {
            errors["email"] = new[] { "Email must be a valid email address." };
        }

        if (errors.Any())
        {
            throw new ValidationException(errors);
        }
    }
}
