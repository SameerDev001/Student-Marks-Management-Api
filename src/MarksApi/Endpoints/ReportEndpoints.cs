using System.Security.Claims;
using MarksApi.Authentication;
using MarksApi.Entities;
using MarksApi.Interfaces;

namespace MarksApi.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/students").WithTags("Reports");

        group.MapGet("/{id:int}/transcript", async (
            int id,
            ClaimsPrincipal user,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var role = user.GetRole();
            var studentId = user.GetStudentId();
            var transcript = await reportService.GetStudentTranscriptAsync(id, studentId, role, ct);
            return Results.Ok(transcript);
        })
        .WithName("GetStudentTranscript")
        .WithSummary("Get transcript for a student with credit-weighted average")
        .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Student));

        return routes;
    }
}
