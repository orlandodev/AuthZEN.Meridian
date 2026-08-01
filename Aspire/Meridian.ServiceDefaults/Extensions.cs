using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
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

    // Shared JWT-bearer wiring for every Meridian API validating tokens issued by
    // the Duende IdentityServer. Resolves the Authority via Aspire service discovery.
    public static TBuilder AddMeridianApiAuthentication<TBuilder>(
        this TBuilder builder, string audience = "meridian.api")
        where TBuilder : IHostApplicationBuilder
    {
        var identityServerUrl = builder.Configuration["services:identityserver:https:0"]
            ?? builder.Configuration["services:identityserver:http:0"];

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
    public static TBuilder AddMeridianOpenApi<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var identityServerUrl = builder.Configuration["services:identityserver:https:0"]
            ?? builder.Configuration["services:identityserver:http:0"];

        builder.Services.AddOpenApiWithAuth(
            authority: identityServerUrl!,
            scopes: new Dictionary<string, string>
            {
                { "meridian.reporting.api", "Meridian Reporting API" },
                { "meridian.expenses.api", "Meridian Expenses API" },
                { "meridian.receipts.api", "Meridian Receipts API" }
            });
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
