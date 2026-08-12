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
        string authority, Dictionary<string, string> scopes, string title, string description) =>
        AddOpenApiWithOAuth(services, scopes, title, description, () => new OpenApiOAuthFlows
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
        string authority, Dictionary<string, string> scopes, string title, string description) =>
        AddOpenApiWithOAuth(services, scopes, title, description, () => new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri($"{authority}/connect/token"),
                Scopes = scopes
            }
        });

    // Shared by both flows above — an "oauth2" security scheme differing only
    // in which OpenApiOAuthFlow is populated. Takes a factory rather than an
    // already-built OpenApiOAuthFlows so that parsing `authority` into a Uri
    // stays deferred until the document is actually requested — a test host
    // with no identityserver configured must still be able to build the app.
    private static IServiceCollection AddOpenApiWithOAuth(
        IServiceCollection services, Dictionary<string, string> scopes, string title, string description,
        Func<OpenApiOAuthFlows> flowsFactory)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                // The document-level title/description Scalar renders at the top of
                // the page, above the per-endpoint list.
                document.Info.Title = title;
                document.Info.Description = description;

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
