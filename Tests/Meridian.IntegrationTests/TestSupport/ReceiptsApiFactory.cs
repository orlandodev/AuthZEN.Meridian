extern alias ReceiptsAssembly;

using AuthZen.Pep;
using Meridian.DataAccess.Receipts;
using ReceiptsAssembly::Meridian.Receipts.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meridian.IntegrationTests.TestSupport;

// In-process Receipts.Api host wired to a real (also in-process) Pdp.Service:
// the two TestServers are chained through their handlers, so no network
// sockets or Duende are involved, but every authorization decision genuinely
// travels Receipts.Api -> HTTP -> Pdp.Service -> PolicyRulesEngine and back.
// Unlike ExpensesApiFactory, ReceiptRepository uses no relational-only EF
// Core feature, so the database can stay on InMemory (see PdpApiFactory for
// the same swap) — no Testcontainers Postgres needed here. Blob storage is
// swapped for FakeReceiptBlobStorage for the same reason: it's orthogonal to
// what's under test, but Program.cs's startup seeder needs something working.
public sealed class ReceiptsApiFactory(PdpApiFactory pdp) : WebApplicationFactory<ReceiptsAssembly::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs's own AddNpgsqlDbContext/AddAzureBlobServiceClient calls
        // run before ConfigureTestServices below and need syntactically valid
        // configuration to not throw — neither is ever actually connected to,
        // since both get replaced with fakes immediately after.
        builder.UseSetting("ConnectionStrings:receiptsdb", "Host=localhost;Database=receiptsdb-test;Username=postgres;Password=postgres");
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        // AddAuthZenPep reads this eagerly at startup (throws if missing) even
        // though the real client-credentials plumbing it configures is never
        // used once IPolicyDecisionClient is replaced below.
        builder.UseSetting("Pep:ClientSecret", "unused-in-tests");

        builder.ConfigureTestServices(services =>
        {
            // Distinct name from PdpApiFactory's PolicyDbContext/ExpensesApiFactory's
            // ExpensesDbContext: EF Core's InMemory provider tracks "created" state
            // by name only, so sharing a name across two models means whichever
            // EnsureCreated() runs second skips its own HasData seeding.
            TestDbContextReplacement.UseInMemory<ReceiptsDbContext>(services, "integration-tests-receiptsdb");

            services.RemoveAll<IReceiptBlobStorage>();
            services.AddSingleton<IReceiptBlobStorage, FakeReceiptBlobStorage>();

            // Upload eligibility (UploadEligibilityHandler) looks up the parent
            // expense via ExpensesLookupClient before ever reaching the PDP call
            // this suite is actually proving — swap its primary handler so that
            // lookup resolves against a small fixed set of known expenses (see
            // StubExpensesLookupHandler) instead of the unresolvable real
            // "https+http://expenses-api" address. This is a concrete class, not
            // an interface, so there's no prior registration to RemoveAll first —
            // re-registering just overrides the primary handler set by Program.cs.
            services.AddHttpClient<ExpensesLookupClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubExpensesLookupHandler());

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
