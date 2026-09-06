using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.IntegrationTests.TestSupport;

// Shared by every *ApiFactory in this project: Aspire's AddNpgsqlDbContext
// registers pooling infrastructure (IDbContextPool<TContext> etc.), not just
// DbContextOptions<TContext> — removing only the options leaves a singleton
// pool depending on a now-missing scoped service. Strips every descriptor
// closing over TContext before re-adding a plain (unpooled) InMemory
// registration under the given name.
internal static class TestDbContextReplacement
{
    public static void UseInMemory<TContext>(IServiceCollection services, string databaseName)
        where TContext : DbContext
    {
        var descriptors = services
            .Where(d => d.ServiceType == typeof(TContext)
                || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(TContext))))
            .ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<TContext>(o => o.UseInMemoryDatabase(databaseName));
    }
}
