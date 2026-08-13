using Meridian.DataAccess.PdP;
using Meridian.Pdp.Service.Endpoints;
using Meridian.Pdp.Service.Pdp;
using Meridian.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianOpenApiClientCredentials(
    title: "Meridian Policy Decision Point (PDP)",
    description: "AuthZEN-compatible access-evaluation service. Backend APIs (Policy Enforcement " +
        "Points) call this service to decide whether a subject may perform an action on a resource.");

// EF Core against the Aspire-provisioned Postgres database "policydb".
builder.AddNpgsqlDbContext<PolicyDbContext>("policydb");
builder.Services.AddScoped<IPolicyEngine, PolicyRulesEngine>();

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
app.UseApiExceptionHandling();
app.MapDefaultEndpoints();
app.MapMeridianOpenApiClientCredentials(new Dictionary<string, string>
{
    { "pdp.evaluate", "Meridian PDP evaluation" }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthZenEndpoints();

await app.Services.MigrateOrEnsureCreatedAsync<PolicyDbContext>();

app.Run();

// Top-level statements generate an internal Program class by default,
// invisible outside this assembly. Re-opening it as public here lets
// Meridian.UnitTests (PdpApiFactory) use WebApplicationFactory<Program> to
// host this app's real startup pipeline (DI, middleware, endpoints)
// in-process for integration tests — the standard pattern for testing
// minimal APIs written with top-level statements.
public partial class Program;
