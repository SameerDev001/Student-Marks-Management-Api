namespace MarksApi.Dtos;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class CreateStudentRequest
{
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UpdateStudentRequest
{
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class StudentResponse
{
    public int Id { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateCourseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
}

public class UpdateCourseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
}

public class CourseResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateEnrollmentRequest
{
    public int StudentId { get; set; }
}

public class EnrollmentResponse
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int? Mark { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
    public string? StudentName { get; set; }
    public string? CourseCode { get; set; }
}

public class UpdateMarkRequest
{
    public int? Mark { get; set; }
}

public class CourseStatsResponse
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public int Enrolled { get; set; }
    public int Graded { get; set; }
    public double? Average { get; set; }
    public int? Highest { get; set; }
    public int? Lowest { get; set; }
    public int Passed { get; set; }
}

public class TranscriptCourseItem
{
    public string Code { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public int? Mark { get; set; }
    public bool Passed { get; set; }
}

public class TranscriptResponse
{
    public int StudentId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<TranscriptCourseItem> Courses { get; set; } = new();
    public double? WeightedAverage { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class PaginationParams
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;
    private int _page = 1;

    public int? Page
    {
        get => _page;
        set => _page = !value.HasValue || value.Value < 1 ? 1 : value.Value;
    }

    public int? PageSize
    {
        get => _pageSize;
        set => _pageSize = !value.HasValue || value.Value < 1 ? 10 : (value.Value > MaxPageSize ? MaxPageSize : value.Value);
    }

    public string? Search { get; set; }
    public string? Sort { get; set; }

    public int GetPage() => _page;
    public int GetPageSize() => _pageSize;
}
