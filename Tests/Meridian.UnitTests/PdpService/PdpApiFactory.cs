extern alias PdpServiceAssembly;

using Meridian.DataAccess.PdP;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.UnitTests.PdpService;

// Meridian.Pdp.Service's Program (a top-level-statement entry point) shares
// its simple name with Meridian.ExpensePortal's, Meridian.Expenses.Api's,
// etc. — all referenced by this same test project — so an unqualified
// WebApplicationFactory<Program> is ambiguous. The `PdpServiceAssembly`
// extern alias (declared on the ProjectReference in the .csproj) pins this
// to Meridian.Pdp.Service's Program specifically.
public sealed class PdpApiFactory : WebApplicationFactory<PdpServiceAssembly::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs's own builder.AddNpgsqlDbContext<PolicyDbContext>("policydb")
        // call runs before ConfigureTestServices below and needs a
        // syntactically valid connection string to not throw — it's never
        // actually connected to, since the DbContextOptions get replaced
        // with the InMemory provider immediately after.
        builder.UseSetting("ConnectionStrings:policydb", "Host=localhost;Database=policydb-test;Username=postgres;Password=postgres");

        // Required by Program.cs (no fallback).
        builder.UseSetting("BusinessHours:TimeZone", "America/New_York");

        builder.ConfigureTestServices(services =>
        {
            // Aspire's AddNpgsqlDbContext registers pooling infrastructure
            // (IDbContextPool<PolicyDbContext> etc.), not just
            // DbContextOptions<PolicyDbContext> — removing only the options
            // leaves a singleton pool depending on a now-missing scoped
            // service. Strip every descriptor closing over PolicyDbContext
            // before re-adding a plain (unpooled) InMemory registration.
            var policyDbDescriptors = services
                .Where(d => d.ServiceType == typeof(PolicyDbContext)
                    || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(PolicyDbContext))))
                .ToList();
            foreach (var descriptor in policyDbDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PolicyDbContext>(o => o.UseInMemoryDatabase("pdp-api-tests"));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
