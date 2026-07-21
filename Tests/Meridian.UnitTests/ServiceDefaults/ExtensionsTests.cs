using Meridian.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Meridian.UnitTests.ServiceDefaults;

// These extension methods operate on real WebApplicationBuilder/WebApplication
// instances rather than interfaces, so tests exercise real (but unstarted,
// unbound) builders instead of Moq mocks.
public class ExtensionsTests
{
    private const string IdentityServerHttpsUrl = "https://identityserver.test";
    private const string IdentityServerHttpUrl = "http://identityserver.test";
    private const string DefaultAudience = "meridian.api";

    private static WebApplicationBuilder CreateBuilder(Dictionary<string, string?>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder();
        if (configuration is not null)
        {
            builder.Configuration.AddInMemoryCollection(configuration);
        }
        return builder;
    }

    private static JwtBearerOptions ResolveJwtBearerOptions(WebApplication app) =>
        app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

    private static List<string?> GetMappedRoutes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToList();

    [Fact]
    public void AddServiceDefaults_ReturnsSameBuilderInstance()
    {
        var builder = CreateBuilder();

        var result = builder.AddServiceDefaults();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddServiceDefaults_RegistersHealthChecks()
    {
        var builder = CreateBuilder();
        builder.AddServiceDefaults();

        var app = builder.Build();

        app.Services.GetService<HealthCheckService>().Should().NotBeNull();
    }

    [Fact]
    public void ConfigureOpenTelemetry_ReturnsSameBuilderInstance()
    {
        var builder = CreateBuilder();

        var result = builder.ConfigureOpenTelemetry();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void ConfigureOpenTelemetry_RegistersTracerAndMeterProviders()
    {
        var builder = CreateBuilder();
        builder.ConfigureOpenTelemetry();

        var app = builder.Build();

        app.Services.GetService<TracerProvider>().Should().NotBeNull();
        app.Services.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://otel-collector.test")]
    public void ConfigureOpenTelemetry_DoesNotThrow_RegardlessOfOtlpEndpointConfiguration(string? otlpEndpoint)
    {
        var builder = CreateBuilder(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlpEndpoint });

        var act = () => builder.ConfigureOpenTelemetry().Build();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AddDefaultHealthChecks_RegistersSelfCheckTaggedLive()
    {
        var builder = CreateBuilder();
        builder.AddDefaultHealthChecks();
        var app = builder.Build();
        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync();

        report.Entries.Should().ContainKey("self");
        report.Entries["self"].Status.Should().Be(HealthStatus.Healthy);
        report.Entries["self"].Tags.Should().Contain("live");
    }

    [Fact]
    public void AddMeridianApiAuthentication_PrefersHttpsAuthority_OverHttp()
    {
        var builder = CreateBuilder(new()
        {
            ["services:identityserver:https:0"] = IdentityServerHttpsUrl,
            ["services:identityserver:http:0"] = IdentityServerHttpUrl
        });
        builder.AddMeridianApiAuthentication();
        var app = builder.Build();

        var options = ResolveJwtBearerOptions(app);

        options.Authority.Should().Be(IdentityServerHttpsUrl);
        options.Audience.Should().Be(DefaultAudience);
    }

    [Fact]
    public void AddMeridianApiAuthentication_FallsBackToHttpAuthority_WhenHttpsAbsent()
    {
        var builder = CreateBuilder(new()
        {
            ["services:identityserver:http:0"] = IdentityServerHttpUrl
        });
        // An HTTP authority is only accepted when RequireHttpsMetadata is relaxed for Development.
        builder.Environment.EnvironmentName = Environments.Development;
        builder.AddMeridianApiAuthentication();
        var app = builder.Build();

        var options = ResolveJwtBearerOptions(app);

        options.Authority.Should().Be(IdentityServerHttpUrl);
    }

    [Fact]
    public void AddMeridianApiAuthentication_LeavesAuthorityNull_WhenNeitherConfigured()
    {
        var builder = CreateBuilder();
        builder.AddMeridianApiAuthentication();
        var app = builder.Build();

        var options = ResolveJwtBearerOptions(app);

        options.Authority.Should().BeNull();
    }

    [Fact]
    public void AddMeridianApiAuthentication_AllowsInsecureHttpMetadata_InDevelopment()
    {
        var builder = CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.AddMeridianApiAuthentication();
        var app = builder.Build();

        var options = ResolveJwtBearerOptions(app);

        options.RequireHttpsMetadata.Should().BeFalse();
    }

    [Fact]
    public void AddMeridianApiAuthentication_RequiresHttpsMetadata_OutsideDevelopment()
    {
        var builder = CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.AddMeridianApiAuthentication();
        var app = builder.Build();

        var options = ResolveJwtBearerOptions(app);

        options.RequireHttpsMetadata.Should().BeTrue();
    }

    [Fact]
    public void AddMeridianApiAuthentication_UsesCustomAudience_WhenProvided()
    {
        const string customAudience = "custom.audience";
        var builder = CreateBuilder();
        builder.AddMeridianApiAuthentication(customAudience);
        var app = builder.Build();

        var options = ResolveJwtBearerOptions(app);

        options.Audience.Should().Be(customAudience);
    }

    [Fact]
    public void MapDefaultEndpoints_MapsHealthAndAliveRoutes_InDevelopment()
    {
        var builder = CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.AddDefaultHealthChecks();
        var app = builder.Build();

        app.MapDefaultEndpoints();

        var routes = GetMappedRoutes(app);
        routes.Should().Contain("/health").And.Contain("/alive");
    }

    [Fact]
    public void MapDefaultEndpoints_DoesNotMapHealthRoutes_InProduction()
    {
        var builder = CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.AddDefaultHealthChecks();
        var app = builder.Build();

        app.MapDefaultEndpoints();

        var routes = GetMappedRoutes(app);
        routes.Should().NotContain("/health").And.NotContain("/alive");
    }
}
