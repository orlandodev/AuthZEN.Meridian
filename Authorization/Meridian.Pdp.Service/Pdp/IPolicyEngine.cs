using AuthZen.Contracts;

namespace Meridian.Pdp.Service.Pdp;

// The pluggable decision core. Stage 2 replaces StubPolicyEngine with a real
// rules engine reading the policy DB; Stage 5 swaps in an OPA/OpenFGA-backed
// engine behind this same interface.
public interface IPolicyEngine
{
    Task<bool> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default);
}
