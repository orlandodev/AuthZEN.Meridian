using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using AuthZen.Contracts;
using Microsoft.Extensions.Logging;

namespace AuthZen.Pep;

// HTTP client that speaks the AuthZEN Access Evaluation API and emits
// OpenTelemetry spans + metrics tagged with the decision. The ActivitySource
// and Meter names match what ServiceDefaults subscribes to ("Meridian.AuthZen").
public sealed class AuthZenPolicyDecisionClient : IPolicyDecisionClient
{
    public static readonly ActivitySource ActivitySource = new("Meridian.AuthZen");
    private static readonly Meter Meter = new("Meridian.AuthZen");
    private static readonly Counter<long> Decisions =
        Meter.CreateCounter<long>("authz.decisions", description: "Authorization decisions by outcome.");

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

        var response = await _http.PostAsJsonAsync("access/v1/evaluation", request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccessEvaluationResponse>(ct);

        var decision = result?.Decision ?? false;
        activity?.SetTag("authz.decision", decision ? "permit" : "deny");
        Decisions.Add(1,
            new KeyValuePair<string, object?>("decision", decision ? "permit" : "deny"),
            new KeyValuePair<string, object?>("action", request.Action.Name),
            new KeyValuePair<string, object?>("resource.type", request.Resource.Type));

        return decision;
    }

    public async Task<IReadOnlyList<bool>> AreAllowedAsync(AccessEvaluationsRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("authz.evaluate.batch", ActivityKind.Client);
        activity?.SetTag("authz.batch.size", request.Evaluations.Count);

        var response = await _http.PostAsJsonAsync("access/v1/evaluations", request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccessEvaluationsResponse>(ct);

        return result?.Evaluations.Select(e => e.Decision).ToList() ?? [];
    }
}
