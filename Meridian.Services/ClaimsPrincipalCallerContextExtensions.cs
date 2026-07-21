using System.Security.Claims;

namespace Meridian.Services;

public static class ClaimsPrincipalCallerContextExtensions
{
    public static CallerContext ToCallerContext(this ClaimsPrincipal user) => new(
        user.GetUserId()!,
        user.GetDepartment(),
        user.IsInRole(Roles.Finance),
        user.IsInRole(Roles.Manager));
}
