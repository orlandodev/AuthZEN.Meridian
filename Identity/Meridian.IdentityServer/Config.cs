using Duende.IdentityModel;
// NOTE: the .Sha256() string extension used below comes from this package.
using Duende.IdentityServer.Models;

namespace Meridian.IdentityServer;

// In-memory configuration for local dev. In later stages this moves to the
// configuration/operational stores backed by the "identitydb" database.
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResource("roles", ["role"]),
        new IdentityResource("org", ["department", "employee_id"])
    ];

    // Least privilege: a token minted for the portal grants access to exactly
    // the APIs it's allowed to call, and a leaked or misused token for one
    // API can't be replayed against the others.
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("meridian.expenses.api", "Meridian Expenses API"),
        new ApiScope("meridian.receipts.api", "Meridian Receipts API"),
        new ApiScope("meridian.reporting.api", "Meridian Reporting API"),

        // Service-to-service only — never granted to the portal. Backend
        // services (PEPs) use this via client credentials to call the PDP.
        // See Meridian.Pdp.Service and AuthZen.Pep.
        new ApiScope("pdp.evaluate", "Meridian PDP evaluation")
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("meridian.expenses.api", "Meridian Expenses API")
        {
            Scopes = { "meridian.expenses.api" },
            UserClaims = { "role", "department", "employee_id" }
        },
        new ApiResource("meridian.receipts.api", "Meridian Receipts API")
        {
            Scopes = { "meridian.receipts.api" },
            UserClaims = { "role", "department", "employee_id" }
        },
        new ApiResource("meridian.reporting.api", "Meridian Reporting API")
        {
            Scopes = { "meridian.reporting.api" },
            UserClaims = { "role", "department", "employee_id" }
        },
        new ApiResource("pdp.evaluate", "Meridian PDP evaluation")
        {
            Scopes = { "pdp.evaluate" }
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // The MVC portal (authorization code + PKCE). Calls all three
        // business APIs on the signed-in user's behalf (the BFF pattern) —
        // needs all three API scopes, but never "pdp.evaluate": the portal
        // has no legitimate reason to talk to the PDP directly.
        new Client
        {
            ClientId = "meridian.portal",
            ClientName = "Meridian Expense Portal",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            // Adjust to the portal's Aspire-assigned URL, or use a fixed dev port.
            RedirectUris = { 
                "https://localhost:59577/signin-oidc", 
                "https://localhost:59575/scalar/", 
                "https://localhost:59576/scalar/", 
                "https://localhost:59578/scalar/" },
            PostLogoutRedirectUris = { "https://localhost:59577/signout-callback-oidc" },
            AllowedScopes =
            {
                "openid", "profile", "roles", "org",
                "meridian.expenses.api", "meridian.receipts.api", "meridian.reporting.api"
            },
            AllowOfflineAccess = true,
            AllowedCorsOrigins = { "https://localhost:59577", "https://localhost:59575", "https://localhost:59576", "https://localhost:59578" }
        },

        // Shared service identity for backend APIs calling the PDP (Expenses,
        // Receipts, and Reporting all authenticate as this client via client
        // credentials — no end user is involved in this hop). One shared
        // client is a deliberate simplification for a teaching repo; split
        // into one client per service later if you want per-service
        // auditability in the PDP's logs/traces instead of relying on OTEL's
        // service.name to tell them apart.
        //
        // DEV SECRET — not for use in production.
        new Client
        {
            ClientId = "meridian.pep",
            ClientName = "Meridian Policy Enforcement Points",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("pep-dev-secret".Sha256()) },
            AllowedScopes = { "pdp.evaluate" }
        }
    ];
}
