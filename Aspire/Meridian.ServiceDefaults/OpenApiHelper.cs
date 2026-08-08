using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Meridian.ServiceDefaults;

/// <summary>
/// Adds an interactive login to the Scalar docs page, using the provided authority and scopes.
/// </summary>
public static class OpenApiHelper
{
    /// <summary>
    /// Authorization-code + PKCE flow. Simulates the Portal's login flow and accessing APIs
    /// on behalf of the signed-in user — for APIs a human delegates to via the Portal.
    /// </summary>
    public static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services,
        string authority, Dictionary<string, string> scopes) =>
        AddOpenApiWithOAuth(services, scopes, () => new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{authority}/connect/authorize"),
                TokenUrl = new Uri($"{authority}/connect/token"),
                Scopes = scopes
            }
        });

    /// <summary>
    /// Client-credentials flow. No end user is involved — for APIs called service-to-service,
    /// where the caller authenticates as itself (e.g. a PEP calling the PDP).
    /// </summary>
    public static IServiceCollection AddOpenApiWithClientCredentialsAuth(this IServiceCollection services,
        string authority, Dictionary<string, string> scopes) =>
        AddOpenApiWithOAuth(services, scopes, () => new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri($"{authority}/connect/token"),
                Scopes = scopes
            }
        });

    // Shared by both flows above — an "oauth2" security scheme differing only
    // in which OpenApiOAuthFlow is populated, so the two callers can never
    // drift on how the scheme/security requirement itself is wired. Takes a
    // factory, not an already-built OpenApiOAuthFlows: the document
    // transformer only runs when something actually requests the OpenAPI
    // document, and building the flows (in particular, parsing `authority`
    // into a Uri) needs to stay deferred until then too — a test host with
    // no identityserver configured must still be able to build the app.
    private static IServiceCollection AddOpenApiWithOAuth(
        IServiceCollection services, Dictionary<string, string> scopes, Func<OpenApiOAuthFlows> flowsFactory)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = flowsFactory()
                };

                document.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("oauth2", document)] = [.. scopes.Keys]
                    }
                ];

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
