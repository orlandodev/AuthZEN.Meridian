using Meridian.DataAccess;
using Meridian.Expenses.Api.Authorization;
using Meridian.Expenses.Api.Endpoints;
using Meridian.ServiceDefaults;
using Meridian.Services;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddMeridianOpenApi();

// EF Core against the Aspire-provisioned Postgres database "expensesdb".
builder.AddNpgsqlDbContext<ExpensesDbContext>("expensesdb");

// Register built-in Minimal API validation
builder.Services.AddValidation();

// --- Authentication: validate JWTs issued by the Duende IdentityServer ---
builder.AddMeridianApiAuthentication(audience: "meridian.expenses.api");

// --- Authorization (Stage 0: all rules live here, in this service) ---
builder.Services.AddAuthorizationBuilder()
    // --- Authorization (Stage 0: all rules live here, in this service) ---
    .AddPolicy(Policies.CanApprove, p =>
        p.RequireRole(Roles.Manager, Roles.Finance))
    // --- Authorization (Stage 0: all rules live here, in this service) ---
    .AddPolicy(Policies.CanViewAll, p =>
        p.RequireRole(Roles.Finance));

builder.Services.AddSingleton<IAuthorizationHandler, OwnerOrPrivilegedHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ApprovalHandler>();

builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMeridianOpenApi(new Dictionary<string, string>
{
    { "meridian.expenses.api", "Resource access: Meridian Expenses API" }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapExpenseEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExpensesDbContext>();
    await ExpensesSeedData.EnsureSeededAsync(db);
}

app.Run();
