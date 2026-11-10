using MarksApi.Dtos;
using MarksApi.Entities;
using MarksApi.Interfaces;

namespace MarksApi.Endpoints;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/enrollments").WithTags("Enrollments and Marks");

        group.MapPut("/{id:int}/mark", async (
            int id,
            UpdateMarkRequest request,
            IEnrollmentService enrollmentService,
            CancellationToken ct) =>
        {
            var updated = await enrollmentService.UpdateMarkAsync(id, request, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateMark")
        .WithSummary("Record or update a student mark for an enrollment")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        group.MapDelete("/{id:int}", async (
            int id,
            IEnrollmentService enrollmentService,
            CancellationToken ct) =>
        {
            await enrollmentService.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteEnrollment")
        .WithSummary("Delete an enrollment")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        return routes;
    }
}
