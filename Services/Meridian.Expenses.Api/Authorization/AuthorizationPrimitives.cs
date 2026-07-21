using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

public static class Policies
{
    public const string CanViewAll = "CanViewAll";   // finance only
}

// ---- Resource-based ownership: an employee may act on their own expense ----
public sealed class OwnerOrPrivilegedRequirement : IAuthorizationRequirement;

// ---- Amount-based approval limit, department-scoped for managers ----
public sealed class ApprovalRequirement : IAuthorizationRequirement;

public static class ApprovalRules
{
    public const decimal ManagerLimit = 5000m;
}
