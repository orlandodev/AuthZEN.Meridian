using Duende.AccessTokenManagement.OpenIdConnect;
using Meridian.ExpensePortal.Services;
using Meridian.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddControllersWithViews();

var identityServerUrl = builder.Configuration["services:identityserver:https:0"]
    ?? builder.Configuration["services:identityserver:http:0"];

// OIDC against Duende IdentityServer.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.Authority = identityServerUrl;
        options.ClientId = "meridian.portal";
        options.ResponseType = "code";
        options.UsePkce = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveTokens = true;

        // Duende keeps the id_token minimal — profile/roles/org claims only
        // come back via the userinfo endpoint. See /diagnostics/claims if
        // name/role ever show up empty again.
        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        foreach (var s in new[]
        {
            "openid", "profile", "roles", "org",
            "meridian.expenses.api", "meridian.receipts.api", "meridian.reporting.api",
            "offline_access"
        })
        {
            options.Scope.Add(s);
        }

        // The OIDC handler only auto-maps a fixed set of userinfo claims
        // (sub, name, given_name, family_name, profile, email) — anything
        // else, including these custom "roles"/"org" scope claims, is
        // silently dropped unless explicitly mapped here.
        options.ClaimActions.MapUniqueJsonKey("role", "role");
        options.ClaimActions.MapUniqueJsonKey("department", "department");
        options.ClaimActions.MapUniqueJsonKey("employee_id", "employee_id");

        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
    });
builder.Services.AddAuthorization();

// Automatic access-token management: attaches the signed-in user's access
// token to every outgoing call and transparently refreshes it via the stored
// refresh token (requires the "offline_access" scope, requested above).
builder.Services.AddOpenIdConnectAccessTokenManagement();

// Typed clients to the business APIs (Aspire service discovery resolves each
// "-api" name).
builder.Services.AddHttpClient<ExpensesApiClient>(c => c.BaseAddress = new("https+http://expenses-api"))
    .AddUserAccessTokenHandler();

builder.Services.AddHttpClient<ReportingApiClient>(c => c.BaseAddress = new("https+http://reporting-api"))
    .AddUserAccessTokenHandler();

builder.Services.AddHttpClient<ReceiptsApiClient>(c => c.BaseAddress = new("https+http://receipts-api"))
    .AddUserAccessTokenHandler();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
