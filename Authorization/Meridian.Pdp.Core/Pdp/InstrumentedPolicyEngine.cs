using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuthZen.Contracts;
using Microsoft.Extensions.Logging;

namespace Meridian.Pdp.Core.Pdp;

// Wraps whichever IPolicyEngine a PDP host runs, so the decision span,
// decision counter, latency histogram, and decision log line are emitted
// identically whether the homegrown or the OPA-backed engine is behind it.
// The PDP is where a decision is actually made, so these are the single
// source of truth for decision telemetry. Same ActivitySource/Meter name
// AuthZenPolicyDecisionClient uses client-side — ServiceDefaults already
// subscribes every service to it, so a trace connects the PEP's outbound span
// with this decision's span.
//
// The span is Internal, not Server: ASP.NET Core's request span is already the
// server span for the inbound call, and this one sits beneath it — once per
// item on the batch endpoint. A deny is a normal outcome and leaves the span
// status unset; only an engine failure (OPA unreachable, policydb down) marks
// it Error and records decision=error, then rethrows.
public sealed partial class InstrumentedPolicyEngine(
    IPolicyEngine inner,
    ILogger<InstrumentedPolicyEngine> logger) : IPolicyEngine
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

    private const string ErrorDecision = "error";

    public async Task<PolicyDecision> EvaluateAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("authz.decide", ActivityKind.Internal);
        activity?.SetTag("authz.subject.id", request.Subject.Id);
        activity?.SetTag("authz.action", request.Action.Name);
        activity?.SetTag("authz.resource.type", request.Resource.Type);
        activity?.SetTag("authz.resource.id", request.Resource.Id);

        var start = Stopwatch.GetTimestamp();
        PolicyDecision result;
        try
        {
            result = await inner.EvaluateAsync(request, ct);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("authz.decision", ErrorDecision);
            activity?.SetTag("error.type", errorType);
            activity?.AddException(exception);

            RecordMeasurements(request, ErrorDecision, policyId: null, errorType, Stopwatch.GetElapsedTime(start));
            LogDecisionFailed(logger, exception, request.Subject.Id, request.Action.Name,
                request.Resource.Type, request.Resource.Id);
            throw;
        }

        var elapsed = Stopwatch.GetElapsedTime(start);
        var decision = result.Decision ? "permit" : "deny";
        activity?.SetTag("authz.decision", decision);
        activity?.SetTag("authz.policy.id", result.PolicyId);

        RecordMeasurements(request, decision, result.PolicyId, errorType: null, elapsed);
        LogDecision(logger, decision, request.Subject.Id, request.Action.Name,
            request.Resource.Type, request.Resource.Id, result.PolicyId);

        return result;
    }

    // Metric tags are deliberately low-cardinality: subject and resource IDs
    // live on the span and the log line only.
    private static void RecordMeasurements(
        AccessEvaluationRequest request, string decision, string? policyId, string? errorType, TimeSpan elapsed)
    {
        // Enabled is false (skipping the tag-list allocation below) when
        // nothing is listening to the "Meridian.AuthZen" meter — mirrors the
        // null-conditional guard the Activity tags get for free.
        if (!Decisions.Enabled && !Duration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { "decision", decision },
            { "action", request.Action.Name },
            { "resource.type", request.Resource.Type },
            { "policy.id", policyId }
        };

        if (errorType is not null)
        {
            tags.Add("error.type", errorType);
        }

        Decisions.Add(1, tags);
        Duration.Record(elapsed.TotalMilliseconds, tags);
    }

    // Written inside the authz.decide activity, so OpenTelemetry logging stamps
    // each record with that span's TraceId/SpanId — filter the Aspire
    // dashboard's structured logs by trace to see every decision a request made.
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Authorization {Decision}: subject {SubjectId} {Action} {ResourceType}/{ResourceId} (policy {PolicyId})")]
    private static partial void LogDecision(
        ILogger logger, string decision, string subjectId, string action,
        string resourceType, string? resourceId, string? policyId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Authorization error: subject {SubjectId} {Action} {ResourceType}/{ResourceId} — policy engine failed")]
    private static partial void LogDecisionFailed(
        ILogger logger, Exception exception, string subjectId, string action,
        string resourceType, string? resourceId);
}
