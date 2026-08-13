using AuthZen.Contracts;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Endpoints;

public static class AuthZenEndpoints
{
    public static void MapAuthZenEndpoints(this IEndpointRouteBuilder app)
    {
        // Metadata discovery stays anonymous, like OIDC's .well-known documents.
        // Field set matches the AuthZEN 1.0 spec's discovery example exactly —
        // no access_evaluations_endpoint field, since the spec doesn't define
        // one for the boxcar path (AuthZen.Pep's client hardcodes that path).
        app.MapGet("/.well-known/authzen-configuration", (HttpContext ctx) =>
        {
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            return Results.Ok(new
            {
                policy_decision_point = baseUrl,
                access_evaluation_endpoint = $"{baseUrl}/access/v1/evaluation"
            });
        })
            .WithTags("AuthZEN")
            .WithSummary("AuthZEN discovery document")
            .WithDescription("Anonymous discovery endpoint advertising this PDP's access-evaluation " +
                "endpoint, per the AuthZEN 1.0 discovery convention.");

        // Single decision.
        app.MapPost("/access/v1/evaluation",
            async (AccessEvaluationRequest request, IPolicyEngine engine, CancellationToken ct) =>
                Results.Ok(new AccessEvaluationResponse { Decision = await engine.EvaluateAsync(request, ct) }))
            .RequireAuthorization()
            .WithTags("AuthZEN")
            .WithSummary("Evaluate a single access request")
            .WithDescription("Accepts one Subject-Action-Resource-Context (SARC) request and returns a " +
                "permit/deny decision from the PDP's rules engine. Callers authenticate as a Policy " +
                "Enforcement Point via client credentials (scope pdp.evaluate).");

        // Boxcarred / batch decisions.
        app.MapPost("/access/v1/evaluations",
            async (AccessEvaluationsRequest request, IPolicyEngine engine, CancellationToken ct) =>
            {
                // Validate every entry up front: a malformed entry is a client
                // error for the whole request, not a reason to 500 or to
                // silently drop the entries evaluated before it.
                var merged = new List<AccessEvaluationRequest>(request.Evaluations.Count);
                foreach (var evaluation in request.Evaluations)
                {
                    if (!TryMergeWithDefaults(request, evaluation, out var mergedRequest, out var error))
                    {
                        return Results.BadRequest(new { error });
                    }

                    merged.Add(mergedRequest);
                }

                var results = new List<AccessEvaluationResponse>(merged.Count);
                foreach (var evaluation in merged)
                {
                    var decision = await engine.EvaluateAsync(evaluation, ct);
                    results.Add(new AccessEvaluationResponse { Decision = decision });
                }

                return Results.Ok(new AccessEvaluationsResponse { Evaluations = results });
            })
            .RequireAuthorization()
            .WithTags("AuthZEN")
            .WithSummary("Evaluate a boxcarred batch of access requests")
            .WithDescription("Accepts multiple SARC entries that may share top-level Subject/Action/" +
                "Resource/Context defaults, and returns one permit/deny decision per entry, in order.");
    }

    // AuthZEN boxcar semantics: a top-level subject/action/resource/context
    // on the batch request is a default that each evaluations[] entry
    // inherits when it omits its own value.
    private static bool TryMergeWithDefaults(
        AccessEvaluationsRequest batch,
        EvaluationEntry entry,
        out AccessEvaluationRequest merged,
        out string? error)
    {
        var subject = entry.Subject ?? batch.Subject;
        var action = entry.Action ?? batch.Action;
        var resource = entry.Resource ?? batch.Resource;

        if (subject is null || action is null || resource is null)
        {
            merged = null!;
            error = subject is null
                ? "No subject provided on the evaluation entry or the batch defaults."
                : action is null
                    ? "No action provided on the evaluation entry or the batch defaults."
                    : "No resource provided on the evaluation entry or the batch defaults.";
            return false;
        }

        merged = new AccessEvaluationRequest
        {
            Subject = subject,
            Action = action,
            Resource = resource,
            Context = entry.Context ?? batch.Context
        };
        error = null;
        return true;
    }
}
