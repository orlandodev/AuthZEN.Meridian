using AuthZen.Contracts;
using Meridian.Pdp.Service;
using Meridian.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<IPolicyEngine, StubPolicyEngine>();

// Callers here are PEPs (the Expenses/Receipts/Reporting APIs), not end
// users — they authenticate as themselves via client credentials (see the
// "meridian.pep" client in IdentityServer's Config.cs). This is deliberately
// a different trust boundary than Portal -> business APIs: the subject being
// evaluated travels in the request body (SARC), not in the caller's own
// token, so a service identity is the correct fit here, not a forwarded user
// token or a token-exchange delegation.
builder.AddMeridianApiAuthentication(audience: "pdp.evaluate");
builder.Services.AddAuthorization();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();

// Metadata discovery stays anonymous — same convention as OIDC/.well-known
// discovery documents generally. Nothing sensitive lives here.
app.MapGet("/.well-known/authzen-configuration", (HttpContext ctx) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    return Results.Ok(new
    {
        policy_decision_point = baseUrl,
        access_evaluation_endpoint = $"{baseUrl}/access/v1/evaluation",
        access_evaluations_endpoint = $"{baseUrl}/access/v1/evaluations"
    });
});

// Single decision.
app.MapPost("/access/v1/evaluation",
    (AccessEvaluationRequest request, IPolicyEngine engine) =>
        Results.Ok(new AccessEvaluationResponse { Decision = engine.Evaluate(request) }))
    .RequireAuthorization();

// Boxcarred / batch decisions.
app.MapPost("/access/v1/evaluations",
    (AccessEvaluationsRequest request, IPolicyEngine engine) =>
    {
        var results = request.Evaluations
            .Select(e => new AccessEvaluationResponse { Decision = engine.Evaluate(e) })
            .ToList();
        return Results.Ok(new AccessEvaluationsResponse { Evaluations = results });
    })
    .RequireAuthorization();

app.Run();
