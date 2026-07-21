using AuthZen.Contracts;

namespace Meridian.Pdp.Service;

// STAGE 0/1 placeholder. Intentionally trivial so the service runs and the
// endpoints are demonstrable. Real policy (roles, ownership, amount limits,
// manager-of relationships) arrives in Stage 2.
public sealed class StubPolicyEngine : IPolicyEngine
{
    public bool Evaluate(AccessEvaluationRequest request)
    {
        // Deny by default. Example: allow anyone to read.
        return request.Action.Name is "can_read" or "read";
    }
}
