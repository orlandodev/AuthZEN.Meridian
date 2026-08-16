using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Expense;

// Replicates Meridian.Expenses.Api's OwnerOrPrivilegedHandler (CanRead) and
// ApprovalHandler (CanDecide), sourced from PolicyDbContext instead of JWT
// claims and an in-process constant.
public static class ExpenseRules
{
    // Defense in depth: Expenses.Api's create path already derives
    // ownerId/department from the caller's claims, so this check is
    // unreachable-false today — but a future "create on behalf of" feature
    // would silently bypass a bare `true`, not a rule that actually checks.
    public static async Task<bool> CanCreate(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct)
    {
        if (!RulePrimitives.IsOwner(request))
        {
            return false;
        }

        var subject = await ws.GetProfileAsync(request.Subject.Id, ct);
        if (subject is null)
        {
            return false;
        }

        var department = RulePrimitives.GetResourceDepartment(request);
        return department is not null
               && string.Equals(department, subject.Department, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> CanRead(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct)
    {
        if (RulePrimitives.IsOwner(request))
        {
            return true;
        }

        var subject = await ws.GetProfileAsync(request.Subject.Id, ct);
        if (subject is null)
        {
            return false;
        }

        if (subject.Role == PolicyConstants.RoleNames.Finance)
        {
            return true;
        }

        if (subject.Role != PolicyConstants.RoleNames.Manager)
        {
            return false;
        }

        var ownerId = RulePrimitives.GetOwnerId(request);
        if (ownerId is null)
        {
            return false;
        }

        // Draft carve-out: preserved exactly, even for a manager who
        // genuinely manages the owner. Fail-closed on a missing status too —
        // a PEP that forgets to set "status" must not be treated as
        // equivalent to "not Draft".
        var status = RulePrimitives.GetResourceStatus(request);
        if (status is null || string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return await ws.IsManagerOfAsync(subject.UserId, ownerId, ct);
    }

    // Submit is owner-only, full stop — no manager/finance carve-out, unlike
    // CanRead. Async only for signature-compatibility with the Rules dictionary
    // in PolicyRulesEngine; owner-ness never needs a DB round trip.
    public static Task<bool> CanSubmit(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct) =>
        Task.FromResult(RulePrimitives.IsOwner(request));

    public static async Task<bool> CanDecide(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct)
    {
        var subject = await ws.GetProfileAsync(request.Subject.Id, ct);
        if (subject is null)
        {
            return false;
        }

        if (subject.Role == PolicyConstants.RoleNames.Finance)
        {
            // unconditional, no limit
            return true;
        }

        if (subject.Role != PolicyConstants.RoleNames.Manager)
        {
            return false;
        }

        var ownerId = RulePrimitives.GetOwnerId(request);
        if (ownerId is null)
        {
            return false;
        }

        if (!string.Equals(RulePrimitives.GetResourceStatus(request), "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!await ws.IsManagerOfAsync(subject.UserId, ownerId, ct))
        {
            return false;
        }

        if (!RulePrimitives.TryGetContextAmount(request, out var amount))
        {
            return false;
        }

        var limit = await ws.GetAmountLimitAsync(PolicyConstants.AmountLimitKeys.ExpenseApproveManagerLimit, ct);
        return amount <= limit;
    }
}
