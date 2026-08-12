using Meridian.DataAccess.Models;
using Microsoft.AspNetCore.Authorization;

namespace Meridian.Expenses.Api.Authorization;

public static class Policies
{
    public const string CanApprove = "CanApprove";   // manager or finance
    public const string CanViewAll = "CanViewAll";   // finance only
}

// ---- Resource-based ownership: an employee may act on their own expense ----
// Delegated to the PDP as ("expense", "read") — see OwnerOrPrivilegedHandler.
public sealed class OwnerOrPrivilegedRequirement : IAuthorizationRequirement;

// ---- Approve/reject decision, delegated to the PDP as ("expense", "approve"|"reject") ----
// Carries the caller's intended outcome so the handler knows which PDP action
// to evaluate; the amount limit itself now lives in the PDP's policy database
// (PolicyConstants.AmountLimitKeys.ExpenseApproveManagerLimit), not here.
public sealed class ApprovalRequirement(ExpenseStatus desiredStatus) : IAuthorizationRequirement
{
    public ExpenseStatus DesiredStatus { get; } = desiredStatus;
}
