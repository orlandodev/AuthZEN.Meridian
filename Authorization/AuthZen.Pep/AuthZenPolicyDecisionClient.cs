using System.Diagnostics;
using System.Net.Http.Json;
using AuthZen.Contracts;
using Microsoft.Extensions.Logging;

namespace AuthZen.Pep;

// HTTP client that speaks the AuthZEN Access Evaluation API and emits an
// OpenTelemetry client span for the outbound call, tagged with the decision.
// The ActivitySource name matches what ServiceDefaults subscribes to
// ("Meridian.AuthZen"), so it correlates with the PDP's server-side span for
// the same call. The "authz.decisions" counter is emitted only by
// PolicyRulesEngine on the PDP side — the PDP is where a decision is actually
// made, so it's the single source of truth for that metric
public sealed class AuthZenPolicyDecisionClient : IPolicyDecisionClient
{
    public static readonly ActivitySource ActivitySource = new("Meridian.AuthZen");

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
