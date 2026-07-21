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

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("meridian.api", "Meridian APIs")
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("meridian.api", "Meridian APIs")
        {
            Scopes = { "meridian.api" },
            UserClaims = { "role", "department", "employee_id" }
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // The MVC portal (authorization code + PKCE).
        new Client
        {
            ClientId = "meridian.portal",
            ClientName = "Meridian Expense Portal",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            // Adjust to the portal's Aspire-assigned URL, or use a fixed dev port.
            RedirectUris = { "https://localhost:59577/signin-oidc" },
            PostLogoutRedirectUris = { "https://localhost:59577/signout-callback-oidc" },
            AllowedScopes = { "openid", "profile", "roles", "org", "meridian.api" },
            AllowOfflineAccess = true
        }
    ];
}
