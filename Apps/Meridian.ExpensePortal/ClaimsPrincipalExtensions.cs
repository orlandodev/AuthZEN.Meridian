using System.Security.Claims;

namespace Meridian.ExpensePortal;

// Mirrors Meridian.Services.ClaimsPrincipalExtensions.GetUserId() on the API
// side. Duplicated rather than referenced: the Portal deliberately has no
// project reference to the DataAccess/Services layer, since it only talks to
// the business APIs over HTTP — see ReceiptsController for the same split.
// Keep both in sync if the claim-mapping fallback ever changes.
public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
}
