using AuthZen.Contracts;

namespace Meridian.AuthZen.ConformanceTests;

// One SARC request, the PDP host clock it is evaluated at, and the decision
// every conforming PDP must return for it.
public sealed record ConformanceCase(AccessEvaluationRequest Request, bool Expected, DateTimeOffset Now);
