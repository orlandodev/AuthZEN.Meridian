using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.Http.Json;
using AuthZen.Contracts;
using Microsoft.Extensions.Logging;

namespace AuthZen.Pep;

// HTTP client that speaks the AuthZEN Access Evaluation API and emits an
// OpenTelemetry client span for the outbound call, tagged with the decision
// and the engine-neutral policy ID the PDP reports in the response context.
// The policy ID stays in telemetry: it's never returned to callers of
// IPolicyDecisionClient, so it can't reach an end user. The ActivitySource
// name matches what ServiceDefaults subscribes to ("Meridian.AuthZen"), so it
// correlates with the PDP's span for the same call.
//
// The "authz.decisions" counter and "authz.decision.duration" histogram are
// emitted only by the PDP's InstrumentedPolicyEngine — the PDP is where a
// decision is actually made, so it's the single source of truth for those
// metrics. The one thing the PDP can never count is a call that didn't reach
// it (PDP down, token rejected, timeout, malformed response), so this client
// owns the "authz.pep.errors" counter for exactly those.
//
// Fails closed: any failure — non-success status, transport error, timeout,
// unreadable or short response — is rethrown, never turned into a permit.
// The calling handler/filter doesn't catch it, so the request ends in the
// API's GlobalExceptionHandler as a 500 rather than ever being allowed.
public sealed class AuthZenPolicyDecisionClient : IPolicyDecisionClient
{
    public static readonly ActivitySource ActivitySource = new("Meridian.AuthZen");

    private static readonly Meter Meter = new("Meridian.AuthZen");

    // Tagged only with low-cardinality values (error.type, operation, action,
    // resource.type) — never subject or resource IDs.
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>(
        "authz.pep.errors",
        description: "Authorization evaluations that failed before a PDP decision was received.");

    private const string MixedTagValue = "mixed";

    private readonly HttpClient _http;
    private readonly ILogger<AuthZenPolicyDecisionClient> _logger;

    public AuthZenPolicyDecisionClient(HttpClient http, ILogger<AuthZenPolicyDecisionClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(AccessEvaluationRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("authz.evaluate", ActivityKind.Client);
        activity?.SetTag("authz.subject.id", request.Subject.Id);
        activity?.SetTag("authz.action", request.Action.Name);
        activity?.SetTag("authz.resource.type", request.Resource.Type);
        activity?.SetTag("authz.resource.id", request.Resource.Id);

        try
        {
            using var response = await _http.PostAsJsonAsync("access/v1/evaluation", request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AccessEvaluationResponse>(ct);

            var decision = result?.Decision ?? false;
            activity?.SetTag("authz.decision", ToDecisionTag(decision));
            activity?.SetTag("authz.policy.id", result?.GetPolicyId());

            return decision;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            RecordFailure(activity, exception, "evaluate", request.Action.Name, request.Resource.Type);
            throw;
        }
    }

    public async Task<IReadOnlyList<bool>> AreAllowedAsync(AccessEvaluationsRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("authz.evaluate.batch", ActivityKind.Client);
        activity?.SetTag("authz.batch.size", request.Evaluations.Count);

        try
        {
            using var response = await _http.PostAsJsonAsync("access/v1/evaluations", request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AccessEvaluationsResponse>(ct);

            var evaluations = result?.Evaluations ?? [];
            if (evaluations.Count != request.Evaluations.Count)
            {
                throw new InvalidOperationException(
                    $"PDP returned {evaluations.Count} decisions for a batch of {request.Evaluations.Count}.");
            }

            RecordBatchDecisions(activity, request, evaluations);
            return evaluations.Select(e => e.Decision).ToList();
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            RecordFailure(
                activity,
                exception,
                "evaluate.batch",
                CommonValue(request.Evaluations.Select(e => (e.Action ?? request.Action)?.Name)),
                CommonValue(request.Evaluations.Select(e => (e.Resource ?? request.Resource)?.Type)));
            throw;
        }
    }

    // OpenTelemetry's error.type convention: the HTTP status code for a
    // non-success response (401/403 usually means the PEP's token was missing
    // or rejected; 5xx a PDP fault), a fixed value for the two transport
    // failures worth alerting on separately, otherwise the exception type.
    private static string ClassifyError(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: { } status } => ((int)status).ToString(CultureInfo.InvariantCulture),
        HttpRequestException => "pdp_unreachable",
        TaskCanceledException or TimeoutException => "timeout",
        _ => exception.GetType().FullName ?? exception.GetType().Name
    };

    private static void RecordFailure(
        Activity? activity, Exception exception, string operation, string? action, string? resourceType)
    {
        var errorType = ClassifyError(exception);

        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("error.type", errorType);
        activity?.AddException(exception);

        if (!Errors.Enabled)
        {
            return;
        }

        Errors.Add(1, new TagList
        {
            { "error.type", errorType },
            { "operation", operation },
            { "action", action },
            { "resource.type", resourceType }
        });
    }

    // One span event per item, carrying the per-item IDs that must never
    // become metric tags. Resolves each entry against the batch defaults the
    // same way the PDP does, so the event shows what was actually evaluated.
    private static void RecordBatchDecisions(
        Activity? activity, AccessEvaluationsRequest request, IReadOnlyList<AccessEvaluationResponse> evaluations)
    {
        if (activity is null || !activity.IsAllDataRequested)
        {
            return;
        }

        var permits = 0;
        for (var i = 0; i < evaluations.Count; i++)
        {
            var entry = request.Evaluations[i];
            var evaluation = evaluations[i];
            var resource = entry.Resource ?? request.Resource;

            if (evaluation.Decision)
            {
                permits++;
            }

            activity.AddEvent(new ActivityEvent("authz.decision", tags: new ActivityTagsCollection
            {
                { "authz.subject.id", (entry.Subject ?? request.Subject)?.Id },
                { "authz.action", (entry.Action ?? request.Action)?.Name },
                { "authz.resource.type", resource?.Type },
                { "authz.resource.id", resource?.Id },
                { "authz.decision", ToDecisionTag(evaluation.Decision) },
                { "authz.policy.id", evaluation.GetPolicyId() }
            }));
        }

        activity.SetTag("authz.batch.permits", permits);
        activity.SetTag("authz.batch.denies", evaluations.Count - permits);
    }

    private static string? CommonValue(IEnumerable<string?> values)
    {
        var distinct = values.Distinct().Take(2).ToList();
        return distinct.Count switch
        {
            0 => null,
            1 => distinct[0],
            _ => MixedTagValue
        };
    }

    private static string ToDecisionTag(bool decision) => decision ? "permit" : "deny";
}
