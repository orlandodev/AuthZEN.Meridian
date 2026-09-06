extern alias PdpAssembly;

using Meridian.DataAccess.PdP;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meridian.IntegrationTests.TestSupport;

// In-process Pdp.Service host: real rules engine, real HTTP pipeline — only
// the database and auth are test doubles. Mirrors
// Meridian.UnitTests/PdpService/PdpApiFactory.cs.
//
// databaseName defaults to the name ExpensesPdpFixture has always used, so
// that fixture's behavior is unchanged. Every other fixture that constructs
// its own PdpApiFactory (e.g. ReceiptsPdpFixture) must pass a distinct name —
// EF Core's InMemory provider shares storage by name process-wide when no
// explicit InMemoryDatabaseRoot is given, so two independent PdpApiFactory
// hosts pointed at the same name would race to seed (and could observe each
// other's data) under xUnit's default cross-collection parallelization.
// timeProvider, when supplied, is registered so PolicyRulesEngine evaluates
// time-of-day rules (DepartmentSpendRules.CanExport's business-hours gate)
// against a fixed instant instead of the wall clock — see ReportingPdpFixture.
public sealed class PdpApiFactory(
    string databaseName = "integration-tests-policydb",
    TimeProvider? timeProvider = null)
    : WebApplicationFactory<PdpAssembly::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs's own builder.AddNpgsqlDbContext<PolicyDbContext>("policydb")
        // call runs before ConfigureTestServices below and needs a syntactically
        // valid connection string to not throw — it's never actually connected
        // to, since the DbContextOptions get replaced with InMemory immediately after.
        builder.UseSetting("ConnectionStrings:policydb", "Host=localhost;Database=policydb-test;Username=postgres;Password=postgres");

        builder.ConfigureTestServices(services =>
        {
            // Distinct name from ExpensesApiFactory's ExpensesDbContext: EF
            // Core's InMemory provider tracks "created" state by name only, so
            // sharing a name across two models means whichever EnsureCreated()
            // runs second skips its own HasData seeding.
            TestDbContextReplacement.UseInMemory<PolicyDbContext>(services, databaseName);

            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }

            services.AddAuthentication(PepTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, PepTestAuthHandler>(PepTestAuthHandler.SchemeName, _ => { });
        });
    }
}
