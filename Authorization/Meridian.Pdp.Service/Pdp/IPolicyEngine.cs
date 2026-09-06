using AuthZen.Contracts;

namespace Meridian.Pdp.Service.Pdp;

// The pluggable decision core. Implementations — a rules engine reading the
// policy DB, or an OPA/OpenFGA-backed engine — swap in behind this interface.
public interface IPolicyEngine
{
    Task<bool> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default);
}
