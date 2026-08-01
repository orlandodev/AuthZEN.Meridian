using Meridian.DataAccess;
using Meridian.Reporting.Api.Authorization;
using Meridian.Reporting.Api.Endpoints;
using Meridian.ServiceDefaults;
using Meridian.Services;

// Reporting API skeleton. Stage 0: authenticated but not yet a PEP. In Stage 4 this
// service gains AuthZen.Pep and delegates every decision to the shared PDP,
// proving one policy enforced across multiple services.
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianOpenApi();

// EF Core against the Aspire-provisioned Postgres database "expensesdb".
builder.AddNpgsqlDbContext<ReportingDbContext>("reportingdb");

// Register built-in Minimal API validation
builder.Services.AddValidation();

builder.AddMeridianApiAuthentication(audience: "meridian.reporting.api");
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanViewDepartmentSpend, p =>
        p.RequireRole(Roles.Manager, Roles.Finance))
    .AddPolicy(Policies.CanExportDepartmentSpend, p =>
        p.RequireRole(Roles.Finance));

builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IReportingRepository, ReportingRepository>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMeridianOpenApi(new Dictionary<string, string>
{
    { "meridian.reporting.api", "Resource access: Meridian Reporting API" }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapReportingEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
    await ReportingSeedData.EnsureSeededAsync(db, TimeProvider.System);
}

app.Run();
