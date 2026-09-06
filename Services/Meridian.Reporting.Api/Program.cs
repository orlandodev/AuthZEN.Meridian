using AuthZen.Pep;
using Meridian.DataAccess.Reporting;
using Meridian.Reporting.Api.Endpoints;
using Meridian.ServiceDefaults;
using Meridian.Services;
using Meridian.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianOpenApi(
    title: "Meridian Reporting API",
    description: "Department spend summaries and CSV export, for Finance and department managers.");

// EF Core against the Aspire-provisioned Postgres database "expensesdb".
builder.AddNpgsqlDbContext<ReportingDbContext>("reportingdb");

// Register built-in Minimal API validation
builder.Services.AddValidation();

// --- Authentication: validate JWTs issued by the Duende IdentityServer ---
builder.AddMeridianApiAuthentication(audience: "meridian.reporting.api");

// --- PEP: this API delegates authorization decisions to the PDP instead of
// enforcing in-process, same as Expenses.Api and Receipts.Api. Authenticates
// to the PDP as itself via client credentials — see the shared "meridian.pep"
// client in IdentityServer's Config.cs.
builder.Services.AddAuthZenPep(
    pdpBaseAddress: "https+http://pdp",
    identityServerTokenEndpoint: "https+http://identityserver/connect/token",
    clientId: "meridian.pep",
    clientSecret: builder.Configuration["Pep:ClientSecret"]
        ?? throw new InvalidOperationException("Missing configuration: Pep:ClientSecret"));

// --- Authorization: the department-scoping and business-hours checks are now
// the PDP's call (DepartmentSpendRules) — see the endpoint filters ---
builder.Services.AddAuthorization();

builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IReportingRepository, ReportingRepository>();

var app = builder.Build();
app.UseApiExceptionHandling();
app.MapDefaultEndpoints();
app.MapMeridianOpenApi(new Dictionary<string, string>
{
    { "meridian.reporting.api", "Resource access: Meridian Reporting API" }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapReportingEndpoints();

await app.Services.MigrateOrEnsureCreatedAsync<ReportingDbContext>();

app.Run();

// Top-level statements generate an internal Program class by default,
// invisible outside this assembly. Re-opening it as public here lets
// Meridian.IntegrationTests use WebApplicationFactory<Program> to host this
// app's real startup pipeline in-process, chained against a real (also
// in-process) Pdp.Service — see Meridian.Expenses.Api/Program.cs for the same
// pattern.
public partial class Program;
