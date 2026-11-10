using System.Security.Claims;
using MarksApi.Authentication;
using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Interfaces;

namespace MarksApi.Endpoints;

public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async (
            [AsParameters] PaginationParams pagination,
            IStudentService studentService,
            CancellationToken ct) =>
        {
            var result = await studentService.GetAllAsync(pagination, ct);
            return Results.Ok(result);
        })
        .WithName("GetStudents")
        .WithSummary("Get paginated list of students")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            IStudentService studentService,
            CancellationToken ct) =>
        {
            var role = user.GetRole();
            var studentId = user.GetStudentId();
            var result = await studentService.GetByIdAsync(id, studentId, role, ct);
            return Results.Ok(result);
        })
        .WithName("GetStudentById")
        .WithSummary("Get student by ID")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Student));

        group.MapPost("/", async (
            CreateStudentRequest request,
            IStudentService studentService,
            CancellationToken ct) =>
        {
            var created = await studentService.CreateAsync(request, ct);
            return Results.Created($"/api/students/{created.Id}", created);
        })
        .WithName("CreateStudent")
        .WithSummary("Create a new student")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapPut("/{id:int}", async (
            int id,
            UpdateStudentRequest request,
            IStudentService studentService,
            CancellationToken ct) =>
        {
            var updated = await studentService.UpdateAsync(id, request, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateStudent")
        .WithSummary("Update an existing student")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapDelete("/{id:int}", async (
            int id,
            IStudentService studentService,
            CancellationToken ct) =>
        {
            await studentService.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteStudent")
        .WithSummary("Delete a student")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        return routes;
    }
}
