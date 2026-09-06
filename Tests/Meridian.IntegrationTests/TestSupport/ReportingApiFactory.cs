extern alias ReportingAssembly;

using AuthZen.Pep;
using Meridian.DataAccess.Reporting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meridian.IntegrationTests.TestSupport;

// In-process Reporting.Api host wired to a real (also in-process) Pdp.Service,
// exactly as ReceiptsApiFactory does for Receipts.Api: the two TestServers are
// chained through their handlers, so every authorization decision genuinely
// travels Reporting.Api -> HTTP -> Pdp.Service -> PolicyRulesEngine and back,
// without sockets or Duende. ReportingRepository uses no relational-only EF
// Core feature, so the database stays on InMemory — no Testcontainers Postgres
// needed here.
public sealed class ReportingApiFactory(PdpApiFactory pdp) : WebApplicationFactory<ReportingAssembly::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs's own AddNpgsqlDbContext call runs before ConfigureTestServices
        // below and needs a syntactically valid connection string to not throw —
        // it's never actually connected to, since the DbContextOptions get
        // replaced with InMemory immediately after.
        builder.UseSetting("ConnectionStrings:reportingdb", "Host=localhost;Database=reportingdb-test;Username=postgres;Password=postgres");
        // AddAuthZenPep reads this eagerly at startup (throws if missing) even
        // though the real client-credentials plumbing it configures is never
        // used once IPolicyDecisionClient is replaced below.
        builder.UseSetting("Pep:ClientSecret", "unused-in-tests");

        builder.ConfigureTestServices(services =>
        {
            // Distinct name from every other InMemory context in this project —
            // EF Core's InMemory provider tracks "created" state by name only, so
            // sharing a name across two models means whichever EnsureCreated()
            // runs second skips its own HasData seeding.
            TestDbContextReplacement.UseInMemory<ReportingDbContext>(services, "integration-tests-reportingdb");

            services.AddAuthentication(EndUserTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, EndUserTestAuthHandler>(EndUserTestAuthHandler.SchemeName, _ => { });

            // Replace the real client-credentials-backed IPolicyDecisionClient
            // with one whose primary handler is the Pdp.Service TestServer's
            // handler, carrying the sentinel auth header PepTestAuthHandler
            // expects — bypassing Duende, not the PDP's real HTTP contract.
            services.RemoveAll<IPolicyDecisionClient>();
            services.AddHttpClient<IPolicyDecisionClient, AuthZenPolicyDecisionClient>(client =>
            {
                client.BaseAddress = new Uri("http://pdp-test/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", PepTestAuthHandler.SentinelHeaderValue);
            }).ConfigurePrimaryHttpMessageHandler(() => pdp.Server.CreateHandler());
        });
    }
}
