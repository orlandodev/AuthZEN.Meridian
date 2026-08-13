extern alias ExpensesAssembly;

using AuthZen.Pep;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meridian.IntegrationTests.TestSupport;

// In-process Expenses.Api host wired to a real (also in-process) Pdp.Service:
// the two TestServers are chained through their handlers, so no network
// sockets or Duende are involved, but every authorization decision genuinely
// travels Expenses.Api -> HTTP -> Pdp.Service -> PolicyRulesEngine and back.
// The database is a real Testcontainers Postgres (see ExpensesPdpFixture),
// not InMemory: TryDecideAsync's ExecuteUpdateAsync is relational-only, and
// Program.cs's own AddNpgsqlDbContext + MigrateOrEnsureCreatedAsync work
// unmodified against it — nothing here needs to swap the DB, only auth and
// the PDP client.
public sealed class ExpensesApiFactory(PdpApiFactory pdp, string expensesDbConnectionString)
    : WebApplicationFactory<ExpensesAssembly::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:expensesdb", expensesDbConnectionString);
        // AddAuthZenPep reads this eagerly at startup (throws if missing) even
        // though the real client-credentials plumbing it configures is never
        // used once IPolicyDecisionClient is replaced below.
        builder.UseSetting("Pep:ClientSecret", "unused-in-tests");

        builder.ConfigureTestServices(services =>
        {
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
