using Meridian.DataAccess;
using Meridian.Expenses.Api.Authorization;
using Meridian.Expenses.Api.Endpoints;
using Meridian.ServiceDefaults;
using Meridian.Services;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// EF Core against the Aspire-provisioned Postgres database "expensesdb".
builder.AddNpgsqlDbContext<ExpensesDbContext>("expensesdb");

// --- Data access + service layers ---
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// --- Request DTO validation (built-in minimal-API DataAnnotations support) ---
builder.Services.AddValidation();

// --- Authentication: validate JWTs issued by the Duende IdentityServer ---
builder.AddMeridianApiAuthentication();

// --- Authorization (Stage 0: all rules live here, in this service) ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.CanViewAll, p =>
        p.RequireRole(Roles.Finance));
});
builder.Services.AddSingleton<IAuthorizationHandler, OwnerOrPrivilegedHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ApprovalHandler>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapExpenseEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExpensesDbContext>();
    await SeedData.EnsureSeededAsync(db);
}

app.Run();
