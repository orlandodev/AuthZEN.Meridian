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

// ---- Story 4.0: upload eligibility, checked against the parent Expense ----
// Deliberately narrower than OwnerOrPrivilegedRequirement: only the literal
// owner may ever upload, and only while the expense is still Draft — Finance
// and managers are never eligible, at any status. Evaluated in-process (not
// via the PDP) since Receipts.Api hasn't been converted to a PEP yet — see
// UploadEligibilityHandler.
public sealed class UploadEligibilityRequirement : IAuthorizationRequirement;
