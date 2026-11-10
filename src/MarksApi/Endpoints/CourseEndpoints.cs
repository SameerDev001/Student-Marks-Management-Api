using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Interfaces;

namespace MarksApi.Endpoints;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/courses").WithTags("Courses");

        group.MapGet("/", async (
            [AsParameters] PaginationParams pagination,
            ICourseService courseService,
            CancellationToken ct) =>
        {
            var result = await courseService.GetAllAsync(pagination, ct);
            return Results.Ok(result);
        })
        .WithName("GetCourses")
        .WithSummary("Get paginated list of courses")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Student));

        group.MapGet("/{id:int}", async (
            int id,
            ICourseService courseService,
            CancellationToken ct) =>
        {
            var result = await courseService.GetByIdAsync(id, ct);
            return Results.Ok(result);
        })
        .WithName("GetCourseById")
        .WithSummary("Get course by ID")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Student));

        group.MapPost("/", async (
            CreateCourseRequest request,
            ICourseService courseService,
            CancellationToken ct) =>
        {
            var created = await courseService.CreateAsync(request, ct);
            return Results.Created($"/api/courses/{created.Id}", created);
        })
        .WithName("CreateCourse")
        .WithSummary("Create a new course")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapPut("/{id:int}", async (
            int id,
            UpdateCourseRequest request,
            ICourseService courseService,
            CancellationToken ct) =>
        {
            var updated = await courseService.UpdateAsync(id, request, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateCourse")
        .WithSummary("Update an existing course")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapDelete("/{id:int}", async (
            int id,
            ICourseService courseService,
            CancellationToken ct) =>
        {
            await courseService.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteCourse")
        .WithSummary("Delete a course")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        // Course enrollments endpoints
        group.MapPost("/{courseId:int}/enrollments", async (
            int courseId,
            CreateEnrollmentRequest request,
            IEnrollmentService enrollmentService,
            CancellationToken ct) =>
        {
            var created = await enrollmentService.EnrollStudentAsync(courseId, request, ct);
            return Results.Created($"/api/courses/{courseId}/enrollments/{created.Id}", created);
        })
        .WithName("EnrollStudentInCourse")
        .WithSummary("Enroll a student in a course")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapGet("/{courseId:int}/enrollments", async (
            int courseId,
            [AsParameters] PaginationParams pagination,
            IEnrollmentService enrollmentService,
            CancellationToken ct) =>
        {
            var result = await enrollmentService.GetByCourseIdAsync(courseId, pagination, ct);
            return Results.Ok(result);
        })
        .WithName("GetCourseEnrollments")
        .WithSummary("Get enrollments for a course")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        // Course statistics endpoint
        group.MapGet("/{courseId:int}/stats", async (
            int courseId,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var stats = await reportService.GetCourseStatsAsync(courseId, ct);
            return Results.Ok(stats);
        })
        .WithName("GetCourseStats")
        .WithSummary("Get statistics for a course")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        return routes;
    }
}
