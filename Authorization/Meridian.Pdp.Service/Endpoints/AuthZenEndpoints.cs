using AuthZen.Contracts;
using Meridian.Pdp.Service.Pdp;

namespace Meridian.Pdp.Service.Endpoints;

public static class AuthZenEndpoints
{
    public static void MapAuthZenEndpoints(this IEndpointRouteBuilder app)
    {
        // Metadata discovery stays anonymous — same convention as OIDC/.well-known
        // discovery documents generally. Nothing sensitive lives here.
        //
        // Field set matches the AuthZEN 1.0 spec's discovery example exactly
        // (policy_decision_point, access_evaluation_endpoint) — no
        // access_evaluations_endpoint field, since the spec doesn't define
        // one for the boxcar path. AuthZen.Pep's client already hardcodes
        // both /access/v1/evaluation and /access/v1/evaluations as literal
        // paths rather than reading them from here, so nothing downstream
        // depends on the extra field.
        app.MapGet("/.well-known/authzen-configuration", (HttpContext ctx) =>
        {
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            return Results.Ok(new
            {
                policy_decision_point = baseUrl,
                access_evaluation_endpoint = $"{baseUrl}/access/v1/evaluation"
            });
        });

        // Single decision.
        app.MapPost("/access/v1/evaluation",
            async (AccessEvaluationRequest request, IPolicyEngine engine, CancellationToken ct) =>
                Results.Ok(new AccessEvaluationResponse { Decision = await engine.EvaluateAsync(request, ct) }))
            .RequireAuthorization();

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
            .RequireAuthorization();
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
