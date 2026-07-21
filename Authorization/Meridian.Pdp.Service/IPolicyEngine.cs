using AuthZen.Contracts;

namespace Meridian.Pdp.Service;

// The pluggable decision core. Stage 2 replaces StubPolicyEngine with a real
// rules engine reading the policy DB; Stage 5 swaps in an OPA/OpenFGA-backed
// engine behind this same interface.
public interface IPolicyEngine
{
    bool Evaluate(AccessEvaluationRequest request);
}
