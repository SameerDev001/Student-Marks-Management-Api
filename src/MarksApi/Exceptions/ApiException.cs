namespace MarksApi.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public ApiException(int statusCode, string title, string? detail = null, IDictionary<string, string[]>? errors = null)
        : base(detail ?? title)
    {
        StatusCode = statusCode;
        Title = title;
        Errors = errors;
    }
}

public class ValidationException : ApiException
{
    public ValidationException(string field, string error)
        : base(400, "One or more validation errors occurred.", null, new Dictionary<string, string[]> { { field, new[] { error } } })
    {
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base(400, "One or more validation errors occurred.", null, errors)
    {
    }
}

public class NotFoundException : ApiException
{
    public NotFoundException(string detail)
        : base(404, "Not Found", detail)
    {
    }
}

public class ConflictException : ApiException
{
    public ConflictException(string title, string detail)
        : base(409, title, detail)
    {
    }

    public ConflictException(string detail)
        : base(409, "Conflict", detail)
    {
    }
}

public class ForbiddenException : ApiException
{
    public ForbiddenException(string detail = "You do not have permission to access this resource.")
        : base(403, "Forbidden", detail)
    {
    }
}

public class UnauthorizedException : ApiException
{
    public UnauthorizedException(string detail = "Invalid credentials.")
        : base(401, "Unauthorized", detail)
    {
    }
}
