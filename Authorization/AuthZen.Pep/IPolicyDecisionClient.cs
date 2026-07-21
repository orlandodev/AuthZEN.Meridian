using AuthZen.Contracts;

namespace AuthZen.Pep;

// The seam every enforcement point calls. In Stage 3 the APIs stop enforcing
// in-process and start calling this instead. Swapping the PDP implementation
// (homegrown -> OPA -> OpenFGA) never changes this interface.
public interface IPolicyDecisionClient
{
    Task<bool> IsAllowedAsync(AccessEvaluationRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<bool>> AreAllowedAsync(AccessEvaluationsRequest request, CancellationToken ct = default);
}
