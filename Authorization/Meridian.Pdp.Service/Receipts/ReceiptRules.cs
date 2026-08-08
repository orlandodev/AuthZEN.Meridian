using AuthZen.Contracts;
using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Receipts;

// Replicates Meridian.Receipts.Api's OwnerOrPrivilegedHandler, but adds the
// manager-of branch that Receipts.Api itself lacks today (Receipt has no
// Department field there — documented Stage-1 drift). This is an
// intentional PDP-only normalization: Receipts.Api's own in-process check is
// left untouched, and nothing calls the PDP yet, so no runtime behavior
// changes anywhere as a result.
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
}
