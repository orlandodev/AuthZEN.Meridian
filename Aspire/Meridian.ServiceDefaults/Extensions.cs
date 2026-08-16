using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

namespace Meridian.ServiceDefaults;

// Shared defaults for every Meridian service: OpenTelemetry, health checks,
// resilient HttpClients, and service discovery. Registered via builder.AddServiceDefaults().
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        // Registered for every service, but only wired into the pipeline by
        // UseApiExceptionHandling() below — the MVC portal never calls that,
        // so it renders its own error views instead.
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });

        return builder;
    }

    // Call first, right after builder.Build(), on every JSON API so it wraps
    // every other middleware and catches whatever's downstream.
    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }

    // Registers the plumbing BearerForwardingHandler needs. Callers then wire
    // it onto a specific outgoing HttpClient with
    // .AddHttpClient<TClient>(...).AddHttpMessageHandler<BearerForwardingHandler>() —
    // see BearerForwardingHandler for what it does and why it's safe only for
    // owner-scoped service-to-service calls.
    public static TBuilder AddBearerForwarding<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<BearerForwardingHandler>();
        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();
                // Meridian custom authorization metrics live here in later stages:
                metrics.AddMeter("Meridian.AuthZen");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();
                // Meridian custom authorization spans (permit/deny) attach to this source:
                tracing.AddSource("Meridian.AuthZen");
            });

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlp = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlp)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    // Aspire service-discovery lookup shared by every method below that talks
    // to IdentityServer. Deliberately non-throwing and possibly-null: a test
    // host with no identityserver resource registered must still build —
    // JwtBearer tolerates a null Authority, and the OpenAPI methods only
    // resolve this lazily, when something actually requests the document.
    private static string? GetIdentityServerUrl(IConfiguration configuration) =>
        configuration["services:identityserver:https:0"]
            ?? configuration["services:identityserver:http:0"];

    // Shared JWT-bearer wiring for every Meridian API validating tokens issued by
    // the Duende IdentityServer. Resolves the Authority via Aspire service discovery.
    public static TBuilder AddMeridianApiAuthentication<TBuilder>(
        this TBuilder builder, string audience = "meridian.api")
        where TBuilder : IHostApplicationBuilder
    {
        var identityServerUrl = GetIdentityServerUrl(builder.Configuration);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = identityServerUrl;  // Aspire service discovery
                options.Audience = audience;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            });

        return builder;
    }

    // Opt-in OpenAPI + Scalar UI for services that expose an HTTP API. Not part of
    // AddServiceDefaults/MapDefaultEndpoints since not every Meridian service (e.g. the
    // MVC portal, IdentityServer) has an API document to publish.
    public static TBuilder AddMeridianOpenApi<TBuilder>(this TBuilder builder, string title, string description)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOpenApiWithAuth(
            authority: GetIdentityServerUrl(builder.Configuration)!,
            scopes: new Dictionary<string, string>
            {
                { "meridian.reporting.api", "Meridian Reporting API" },
                { "meridian.expenses.api", "Meridian Expenses API" },
                { "meridian.receipts.api", "Meridian Receipts API" }
            },
            title: title,
            description: description);
        return builder;
    }

    // Exposes the OpenAPI document at /openapi/v1.json and the Scalar UI at /scalar/v1,
    // Development only — these are exploration/debugging surfaces, not for production traffic.
    public static WebApplication MapMeridianOpenApi(this WebApplication app, Dictionary<string, string> oauthScopes)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options
                .AddPreferredSecuritySchemes("oauth2", "Meridian OAuth2")
                .AddAuthorizationCodeFlow("oauth2", flow =>
                {
                    flow.ClientId = "meridian.portal";
                    flow.Pkce = Pkce.Sha256;
                    flow.SelectedScopes = [.. oauthScopes.Keys];
                }));
        }
        return app;
    }

    // Client-credentials counterpart to AddMeridianOpenApi, for services with no
    // user-delegated caller — e.g. the PDP, called by PEPs authenticating as themselves.
    public static TBuilder AddMeridianOpenApiClientCredentials<TBuilder>(
        this TBuilder builder, string title, string description)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOpenApiWithClientCredentialsAuth(
            authority: GetIdentityServerUrl(builder.Configuration)!,
            scopes: new Dictionary<string, string>
            {
                { "pdp.evaluate", "Meridian PDP evaluation" }
            },
            title: title,
            description: description);
        return builder;
    }

    // Client-credentials counterpart to MapMeridianOpenApi. Scalar's interactive "Try it"
    // login uses the shared "meridian.pep" dev client (see IdentityServer's Config.cs) —
    // the same client-credentials grant real PEP callers use, so testing via Scalar
    // exercises the actual auth path instead of a stand-in for it.
    // DEV SECRET, hardcoded — this surface is Development-only (guarded below) and the
    // secret is already documented as dev-only where the client is registered.
    public static WebApplication MapMeridianOpenApiClientCredentials(this WebApplication app, Dictionary<string, string> oauthScopes)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options
                .AddPreferredSecuritySchemes("oauth2", "Meridian OAuth2")
                .AddClientCredentialsFlow("oauth2", flow =>
                {
                    flow.ClientId = "meridian.pep";
                    flow.ClientSecret = "pep-dev-secret";
                    flow.SelectedScopes = [.. oauthScopes.Keys];
                }));
        }
        return app;
    }

    // Real (Npgsql) databases get migrated; a test host on the InMemory
    // provider falls back to EnsureCreated() instead, since InMemory doesn't
    // support Migrate(). HasData seeding works either way.
    public static async Task MigrateOrEnsureCreatedAsync<TContext>(this IServiceProvider services)
        where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/alive", new()
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }
        return app;
    }
}
