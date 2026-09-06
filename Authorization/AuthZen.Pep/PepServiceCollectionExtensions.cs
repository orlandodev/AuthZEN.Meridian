using Duende.AccessTokenManagement;
using Microsoft.Extensions.DependencyInjection;

namespace AuthZen.Pep;

public static class PepServiceCollectionExtensions
{
    // One-line registration so an API becomes a PEP that authenticates to the
    // PDP as itself via client credentials — never by forwarding an end
    // user's own token. The PDP trusts services here, not users; see the
    // "meridian.pep" client in IdentityServer's Config.cs for the
    // corresponding registration.
    //
    //   builder.Services.AddAuthZenPep(
    //       pdpBaseAddress: "https+http://pdp",
    //       identityServerTokenEndpoint: "https+http://identityserver/connect/token",
    //       clientId: "meridian.pep",
    //       clientSecret: <from configuration/user-secrets, never hardcoded>);
    public static IServiceCollection AddAuthZenPep(
        this IServiceCollection services,
        string pdpBaseAddress,
        string identityServerTokenEndpoint,
        string clientId,
        string clientSecret,
        string scope = "pdp.evaluate")
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient("pdp", client =>
            {
                client.TokenEndpoint = new Uri(identityServerTokenEndpoint);

                if (!ClientId.TryParse(clientId, out var parsedClientId, out var clientIdErrors))
                {
                    throw new ArgumentException($"Invalid client id: {string.Join(", ", clientIdErrors)}", nameof(clientId));
                }

                client.ClientId = parsedClientId;

                if (!ClientSecret.TryParse(clientSecret, out var parsedClientSecret, out var clientSecretErrors))
                {
                    throw new ArgumentException($"Invalid client secret: {string.Join(", ", clientSecretErrors)}", nameof(clientSecret));
                }

                client.ClientSecret = parsedClientSecret;

                if (!Scope.TryParse(scope, out var parsedScope, out var scopeErrors))
                {
                    throw new ArgumentException($"Invalid scope: {string.Join(", ", scopeErrors)}", nameof(scope));
                }

                client.Scope = parsedScope;
            });

        if (!ClientCredentialsClientName.TryParse("pdp", out var parsedClientName, out var clientNameErrors))
        {
            throw new ArgumentException($"Invalid client name: {string.Join(", ", clientNameErrors)}", nameof(parsedClientName));
        }

        services.AddClientCredentialsHttpClient("pdp-pep", parsedClientName.Value, client =>
            {
                client.BaseAddress = new Uri(pdpBaseAddress);
            })
            .AddTypedClient<IPolicyDecisionClient, AuthZenPolicyDecisionClient>();

        return services;
    }
}
