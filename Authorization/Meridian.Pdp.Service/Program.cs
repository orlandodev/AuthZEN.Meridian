using AuthZen.Contracts;
using Meridian.Pdp.Service;
using Meridian.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<IPolicyEngine, StubPolicyEngine>();

var app = builder.Build();
app.MapDefaultEndpoints();

// AuthZEN PDP metadata (.well-known). Advertises which endpoints this PDP exposes.
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
        Results.Ok(new AccessEvaluationResponse { Decision = engine.Evaluate(request) }));

// Boxcarred / batch decisions.
app.MapPost("/access/v1/evaluations",
    (AccessEvaluationsRequest request, IPolicyEngine engine) =>
    {
        var results = request.Evaluations
            .Select(e => new AccessEvaluationResponse { Decision = engine.Evaluate(e) })
            .ToList();
        return Results.Ok(new AccessEvaluationsResponse { Evaluations = results });
    });

app.Run();
