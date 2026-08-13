using AuthZen.Pep;
using Meridian.DataAccess.Expenses;
using Meridian.Expenses.Api.Authorization;
using Meridian.Expenses.Api.Endpoints;
using Meridian.ServiceDefaults;
using Meridian.Services;
using Meridian.Services.Contracts;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianOpenApi(
    title: "Meridian Expenses API",
    description: "Create, read, and decide (approve or reject) expense reports. " +
        "Authorization for individual expenses is delegated to the Meridian Policy Decision Point (PDP).");

// EF Core against the Aspire-provisioned Postgres database "expensesdb".
builder.AddNpgsqlDbContext<ExpensesDbContext>("expensesdb");

// Register built-in Minimal API validation
builder.Services.AddValidation();

// --- Authentication: validate JWTs issued by the Duende IdentityServer ---
builder.AddMeridianApiAuthentication(audience: "meridian.expenses.api");

// --- PEP: this API delegates authorization decisions to the PDP instead of
// enforcing in-process (Stage 3). Authenticates to the PDP as itself via
// client credentials — see the "meridian.pep" client in IdentityServer's
// Config.cs.
builder.Services.AddAuthZenPep(
    pdpBaseAddress: "https+http://pdp",
    identityServerTokenEndpoint: "https+http://identityserver/connect/token",
    clientId: "meridian.pep",
    clientSecret: builder.Configuration["Pep:ClientSecret"]
        ?? throw new InvalidOperationException("Missing configuration: Pep:ClientSecret"));

// --- Authorization: declarative role policies stay in-process (unchanged by
// Stage 3 — only the resource-based handlers below now delegate to the PDP) ---
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanApprove, p =>
        p.RequireRole(Roles.Manager, Roles.Finance))
    .AddPolicy(Policies.CanViewAll, p =>
        p.RequireRole(Roles.Finance));

// Scoped, not Singleton: each handler now holds an IPolicyDecisionClient
// backed by an HttpClient, and a Singleton would capture it (and its
// eventually-stale connection) for the life of the app.
builder.Services.AddScoped<IAuthorizationHandler, OwnerOrPrivilegedHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ApprovalHandler>();
builder.Services.AddScoped<ExpenseVisibilityFilter>();

builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

var app = builder.Build();
app.UseApiExceptionHandling();
app.MapDefaultEndpoints();
app.MapMeridianOpenApi(new Dictionary<string, string>
{
    { "meridian.expenses.api", "Resource access: Meridian Expenses API" }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapExpenseEndpoints();

await app.Services.MigrateOrEnsureCreatedAsync<ExpensesDbContext>();

app.Run();

// Top-level statements generate an internal Program class by default,
// invisible outside this assembly. Re-opening it as public here lets
// Meridian.IntegrationTests use WebApplicationFactory<Program> to host this
// app's real startup pipeline in-process, chained against a real (also
// in-process) Pdp.Service — see Meridian.Pdp.Service/Program.cs for the same
// pattern.
public partial class Program;
