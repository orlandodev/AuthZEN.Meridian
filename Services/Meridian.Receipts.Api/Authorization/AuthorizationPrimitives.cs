using Microsoft.AspNetCore.Authorization;

namespace Meridian.Receipts.Api.Authorization;

// ---- Resource-based ownership: an employee may act on their own receipt ----
// Copy-pasted from Expenses.Api's AuthorizationPrimitives.cs (Stage 1: duplicate the
// rules into Receipts.Api rather than sharing them — see OwnerOrPrivilegedHandler.cs
// for the drift this duplication deliberately introduces). No ApprovalRequirement here
// — receipts have no approval workflow — and no named policies, since Expenses.Api's
// own CanApprove/CanViewAll policies are registered but never actually referenced by
// any endpoint; this project doesn't repeat that dead code.
public sealed class OwnerOrPrivilegedRequirement : IAuthorizationRequirement;
