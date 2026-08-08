using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess;

// Shared by every IDesignTimeDbContextFactory in this project. `dotnet ef`
// tooling only needs *a* syntactically valid Npgsql connection string to pick
// the right provider/dialect for `migrations add` — it never actually
// connects. Real connection resolution at runtime still goes through
// Aspire's AddNpgsqlDbContext in each service's Program.cs.
internal static class DesignTimeDbContextOptions
{
    public static DbContextOptions<TContext> Build<TContext>(string databaseName)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql($"Host=localhost;Database={databaseName};Username=postgres;Password=postgres")
            .Options;
}
