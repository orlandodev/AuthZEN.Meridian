namespace Meridian.Pdp.Core.Pdp;

// An engine's verdict plus the engine-neutral ID (see PolicyIds) of the policy
// that governed it. PolicyId is null only when no policy matched the
// (resource.type, action) pair — a default deny.
public sealed record PolicyDecision(bool Decision, string? PolicyId);
