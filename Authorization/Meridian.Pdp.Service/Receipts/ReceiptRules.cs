using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Receipts;

// Replicates what Meridian.Receipts.Api's OwnerOrPrivilegedHandler used to
// check in-process, but adds the manager-of branch that handler itself
// lacked (Receipt has no Department field there — documented Stage-1 drift).
// As of Stage 4 (Story 4.1), OwnerOrPrivilegedHandler and
// UploadEligibilityHandler both delegate to this PDP rule instead of
// enforcing in-process — the manager-of branch is now a live behavior
// change, not just a dormant normalization.
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

    // Story 4.1: mirrors Receipts.Api's own UploadEligibilityHandler exactly —
    // owner-only, and only while the parent expense (the resource here; no
    // Receipt exists yet at upload time) is still Draft. No Finance/manager
    // carve-out, unlike CanRead above: Story 4.0 deliberately made upload
    // narrower than view, and this rule preserves that instead of reusing
    // CanRead's broader owner-or-privileged shape.
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
