using System.Net;
using System.Text.Json;
using MarksApi.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        if (exception is ApiException apiEx)
        {
            context.Response.StatusCode = apiEx.StatusCode;

            var problem = new ProblemDetails
            {
                Status = apiEx.StatusCode,
                Title = apiEx.Title,
                Detail = apiEx.Message,
                Instance = context.Request.Path
            };

            if (apiEx.Errors != null && apiEx.Errors.Any())
            {
                problem.Extensions["errors"] = apiEx.Errors;
            }

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await context.Response.WriteAsync(json);
            return;
        }

        if (exception is DbUpdateException dbUpdateEx && dbUpdateEx.InnerException is PostgresException pgEx)
        {
            // 23505 = unique_violation
            if (pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Duplicate resource",
                    Detail = "A record with unique field(s) or enrollment already exists.",
                    Instance = context.Request.Path
                };
                if (!string.IsNullOrEmpty(pgEx.ConstraintName))
                {
                    if (pgEx.ConstraintName.Contains("registration_no", StringComparison.OrdinalIgnoreCase))
                    {
                        problem.Title = "Duplicate student";
                        problem.Detail = "A student with this registration number already exists.";
                    }
                    else if (pgEx.ConstraintName.Contains("email", StringComparison.OrdinalIgnoreCase))
                    {
                        problem.Title = "Duplicate student";
                        problem.Detail = "A student with this email address already exists.";
                    }
                    else if (pgEx.ConstraintName.Contains("code", StringComparison.OrdinalIgnoreCase))
                    {
                        problem.Title = "Duplicate course";
                        problem.Detail = "A course with this code already exists.";
                    }
                    else if (pgEx.ConstraintName.Contains("student_id_course_id", StringComparison.OrdinalIgnoreCase) ||
                             pgEx.ConstraintName.Contains("enrollments", StringComparison.OrdinalIgnoreCase))
                    {
                        problem.Title = "Duplicate enrollment";
                        problem.Detail = "A student can be enrolled in a course only once.";
                    }
                }

                _logger.LogWarning("Database unique constraint violation: {Message}", pgEx.MessageText);
                var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await context.Response.WriteAsync(json);
                return;
            }

            // 23503 = foreign_key_violation
            if (pgEx.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Deletion or reference conflict",
                    Detail = "The record cannot be deleted or referenced because active relationships exist.",
                    Instance = context.Request.Path
                };

                _logger.LogWarning("Database foreign key violation: {Message}", pgEx.MessageText);
                var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await context.Response.WriteAsync(json);
                return;
            }

            // 23514 = check_violation
            if (pgEx.SqlState == PostgresErrorCodes.CheckViolation)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation error",
                    Detail = "A check constraint was violated by the input data.",
                    Instance = context.Request.Path
                };

                _logger.LogWarning("Database check constraint violation: {Message}", pgEx.MessageText);
                var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await context.Response.WriteAsync(json);
                return;
            }
        }

        // Unhandled 500 error
        _logger.LogError(exception, "An unhandled exception occurred processing request {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var genericProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Detail = "An unexpected error occurred. Please try again later.",
            Instance = context.Request.Path
        };

        var genericJson = JsonSerializer.Serialize(genericProblem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(genericJson);
    }
}
