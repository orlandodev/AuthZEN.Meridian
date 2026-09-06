using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Receipts;

// The receipt read rule. Adds a manager-of branch that Receipts.Api's own
// OwnerOrPrivilegedHandler lacks (Receipt has no Department field there to key
// off). OwnerOrPrivilegedHandler and UploadEligibilityHandler both delegate to
// this rule instead of enforcing in-process, so the manager-of branch is a
// live behavior, not a dormant normalization.
public static class ReceiptRules
{
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
        return ownerId is not null && await ws.IsManagerOfAsync(subject.UserId, ownerId, ct);
    }

    // Mirrors Receipts.Api's own UploadEligibilityHandler exactly — owner-only,
    // and only while the parent expense (the resource here; no Receipt exists
    // yet at upload time) is still Draft. No Finance/manager carve-out, unlike
    // CanRead above: upload is deliberately narrower than view, so this rule
    // does not reuse CanRead's broader owner-or-privileged shape.
    public static Task<bool> CanCreate(AccessEvaluationRequest request, RuleWorkspace ws, CancellationToken ct)
    {
        if (!RulePrimitives.IsOwner(request))
        {
            return Task.FromResult(false);
        }

        var status = RulePrimitives.GetResourceStatus(request);
        return Task.FromResult(status is not null && string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase));
    }
}
