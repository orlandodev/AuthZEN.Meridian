using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.IntegrationTests.TestSupport;

// Stands in for the PDP's real client-credentials-authenticated PEP caller
// (the "meridian.pep" client, normally minted by Duende). Only succeeds when
// the request carries the sentinel Authorization header — requests without
// it still fail authentication, so RequireAuthorization() on the PDP's
// endpoints is genuinely exercised, not bypassed.
public sealed class PepTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestPep";
    public const string SentinelHeaderValue = "TestPep meridian-pep";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.Authorization != SentinelHeaderValue)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "meridian-pep") };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
