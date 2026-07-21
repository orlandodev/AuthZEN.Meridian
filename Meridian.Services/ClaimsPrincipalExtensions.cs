using System.Security.Claims;

namespace Meridian.Services;

// ---- Shared claim helpers ----
// Duende keeps the id_token minimal, so "sub" is the fallback for the
// standard NameIdentifier claim across every call site that needs it.
public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;

    public static string? GetDepartment(this ClaimsPrincipal user) =>
        user.FindFirst("department")?.Value;
}
