using System.Security.Claims;
using MarksApi.Entities;

namespace MarksApi.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var id) ? id : null;
    }

    public static string GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Role)?.Value ??
               user.FindFirst("role")?.Value ??
               string.Empty;
    }

    public static int? GetStudentId(this ClaimsPrincipal user)
    {
        var studentIdClaim = user.FindFirst("studentId")?.Value;
        return int.TryParse(studentIdClaim, out var id) ? id : null;
    }
}
