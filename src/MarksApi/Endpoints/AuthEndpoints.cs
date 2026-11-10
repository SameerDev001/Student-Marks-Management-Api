using MarksApi.Dtos;
using MarksApi.Interfaces;

namespace MarksApi.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken ct) =>
        {
            var result = await authService.LoginAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithSummary("Authenticate user and retrieve JWT token")
        .AllowAnonymous();

        return routes;
    }
}
