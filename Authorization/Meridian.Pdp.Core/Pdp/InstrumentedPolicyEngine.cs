using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuthZen.Contracts;

namespace Meridian.Pdp.Core.Pdp;

// Wraps whichever IPolicyEngine a PDP host runs, so the decision span,
// decision counter, and latency histogram are emitted identically whether the
// homegrown or the OPA-backed engine is behind it. The PDP is where a decision
// is actually made, so these are the single source of truth for decision
// telemetry. Same ActivitySource/Meter name AuthZenPolicyDecisionClient uses
// client-side — ServiceDefaults already subscribes every service to it, so a
// trace connects the PEP's outbound span with this decision's span.
public sealed class InstrumentedPolicyEngine(IPolicyEngine inner) : IPolicyEngine
{
    private static readonly ActivitySource ActivitySource = new("Meridian.AuthZen");
    private static readonly Meter Meter = new("Meridian.AuthZen");

    private static readonly Counter<long> Decisions =
        Meter.CreateCounter<long>("authz.decisions", description: "Authorization decisions by outcome.");

    // A decision typically takes ~1–50 ms; OpenTelemetry's default buckets
    // (0, 5, 10, 25, 50, 75, 100, 250, ... 10000) would lump almost every
    // measurement into the first few.
    private static readonly Histogram<double> Duration = Meter.CreateHistogram(
        "authz.decision.duration",
        unit: "ms",
        description: "Time the policy engine took to reach a decision.",
        tags: null,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [1, 2, 3, 5, 7.5, 10, 15, 20, 30, 50, 75, 100, 250]
        });

    public async Task<PolicyDecision> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("authz.decide", ActivityKind.Server);
        activity?.SetTag("authz.subject.id", request.Subject.Id);
        activity?.SetTag("authz.action", request.Action.Name);
        activity?.SetTag("authz.resource.type", request.Resource.Type);
        activity?.SetTag("authz.resource.id", request.Resource.Id);

        var start = Stopwatch.GetTimestamp();
        var result = await inner.EvaluateAsync(request, ct);
        var elapsed = Stopwatch.GetElapsedTime(start);

        var decision = result.Decision ? "permit" : "deny";
        activity?.SetTag("authz.decision", decision);
        activity?.SetTag("authz.policy.id", result.PolicyId);

        // Enabled is false (skipping the tag-list allocation below) when
        // nothing is listening to the "Meridian.AuthZen" meter — mirrors the
        // null-conditional guard the Activity tags above get for free.
        if (Decisions.Enabled || Duration.Enabled)
        {
            var tags = new TagList
            {
                { "decision", decision },
                { "action", request.Action.Name },
                { "resource.type", request.Resource.Type },
                { "policy.id", result.PolicyId }
            };
            Decisions.Add(1, tags);
            Duration.Record(elapsed.TotalMilliseconds, tags);
        }

        return result;
    }
}
