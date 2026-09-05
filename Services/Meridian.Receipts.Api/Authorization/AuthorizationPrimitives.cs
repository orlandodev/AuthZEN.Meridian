using Microsoft.AspNetCore.Authorization;

namespace Meridian.Receipts.Api.Authorization;

// ---- Resource-based ownership: an employee may act on their own receipt ----
// Stage 1 duplicated Expenses.Api's equivalent handler in-process, which is
// what let the manager-of branch drift out of sync (Receipt has no
// Department field to key off). Stage 4 (Story 4.1) delegates the decision
// itself to the PDP's ReceiptRules.CanRead — see OwnerOrPrivilegedHandler —
// so the requirement type stays, but nothing here evaluates it anymore. No
// ApprovalRequirement here — receipts have no approval workflow — and no
// named policies, since Expenses.Api's own CanApprove/CanViewAll policies are
// registered but never actually referenced by any endpoint; this project
// doesn't repeat that dead code.
public sealed class OwnerOrPrivilegedRequirement : IAuthorizationRequirement;

// ---- Story 4.0/4.1: upload eligibility, checked against the parent Expense ----
// Deliberately narrower than OwnerOrPrivilegedRequirement: only the literal
// owner may ever upload, and only while the expense is still Draft — Finance
// and managers are never eligible, at any status. Story 4.0 evaluated this
// in-process; Story 4.1 delegates it to the PDP's ReceiptRules.CanCreate
// instead — see UploadEligibilityHandler.
public sealed class UploadEligibilityRequirement : IAuthorizationRequirement;
