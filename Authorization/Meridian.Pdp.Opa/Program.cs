using Meridian.DataAccess.PdP;
using Meridian.Pdp.Core.Endpoints;
using Meridian.Pdp.Core.Pdp;
using Meridian.Pdp.Opa.Pdp;
using Meridian.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianOpenApiClientCredentials(
    title: "Meridian Policy Decision Point (PDP) - OPA",
    description: "AuthZEN-compatible access-evaluation service, identical in contract to the " +
        "homegrown PDP but backed by a real Open Policy Agent server evaluating Rego policies. " +
        "See policies/ and README.md in this project for how a decision is actually made.");

// Same policydb this host reads facts from (subject role/department,
// manager-of, amount limits) that the homegrown PDP's RuleWorkspace reads —
// the decision itself moves to Rego, but the facts still come from here.
builder.AddNpgsqlDbContext<PolicyDbContext>("policydb");

// Same requirement as Meridian.Pdp.Service: no fallback, so a missing/unknown
// zone fails loudly at startup rather than silently deciding business-hours
// exports in the wrong timezone.
var timeZoneId = builder.Configuration["BusinessHours:TimeZone"];
if (string.IsNullOrWhiteSpace(timeZoneId))
{
    throw new InvalidOperationException("Missing configuration: BusinessHours:TimeZone");
}

var businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
builder.Services.AddSingleton(businessTimeZone);

// The real OPA server (see AppHost.cs's "opa" container resource and this
// project's README.md) resolved via Aspire service discovery, the same way
// every PEP resolves "pdp" itself.
builder.Services.AddHttpClient<IPolicyEngine, OpaPolicyEngine>(client =>
{
    client.BaseAddress = new Uri("http://opa");
});

// Callers here are PEPs, exactly as with the homegrown PDP — this host
// advertises the identical pdp.evaluate-scoped client-credentials contract,
// so no PEP or AuthZen.Pep code path can tell which engine is behind "pdp".
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

// Public for the same reason as Meridian.Pdp.Service's Program: lets
// WebApplicationFactory<Program> host this app's real startup pipeline
// in-process from a test project.
public partial class Program;
