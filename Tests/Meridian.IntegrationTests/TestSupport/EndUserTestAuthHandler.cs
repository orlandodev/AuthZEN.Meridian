using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.IntegrationTests.TestSupport;

// Stands in for a real JWT-bearer end user signed in via Duende. Reads the
// caller's identity from test-only headers instead of validating a token, so
// each test can impersonate a different user (owner, manager, finance) over
// real HTTP without standing up IdentityServer.
public sealed class EndUserTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestUser";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string RoleHeader = "X-Test-Role";
    public const string DepartmentHeader = "X-Test-Department";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId!) };
        if (Request.Headers.TryGetValue(RoleHeader, out var role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role!));
        }
        if (Request.Headers.TryGetValue(DepartmentHeader, out var department))
        {
            claims.Add(new Claim("department", department!));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
