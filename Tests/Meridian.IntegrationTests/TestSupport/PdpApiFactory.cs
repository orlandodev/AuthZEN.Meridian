extern alias PdpAssembly;

using Meridian.DataAccess.PdP;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.IntegrationTests.TestSupport;

// In-process Pdp.Service host: real rules engine, real HTTP pipeline — only
// the database and auth are test doubles. Mirrors
// Meridian.UnitTests/PdpService/PdpApiFactory.cs.
public sealed class PdpApiFactory : WebApplicationFactory<PdpAssembly::Program>
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
            var policyDbDescriptors = services
                .Where(d => d.ServiceType == typeof(PolicyDbContext)
                    || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(PolicyDbContext))))
                .ToList();
            foreach (var descriptor in policyDbDescriptors)
            {
                services.Remove(descriptor);
            }

            // Distinct name from ExpensesApiFactory's ExpensesDbContext: EF
            // Core's InMemory provider tracks "created" state by name only, so
            // sharing a name across two models means whichever EnsureCreated()
            // runs second skips its own HasData seeding.
            services.AddDbContext<PolicyDbContext>(o => o.UseInMemoryDatabase("integration-tests-policydb"));

            services.AddAuthentication(PepTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, PepTestAuthHandler>(PepTestAuthHandler.SchemeName, _ => { });
        });
    }
}
