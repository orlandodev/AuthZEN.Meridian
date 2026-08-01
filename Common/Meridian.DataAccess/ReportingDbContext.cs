using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess;

public class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<DepartmentSpendSummary> DepartmentSpendSummaries => Set<DepartmentSpendSummary>();
}

public static class ReportingSeedData
{
    // These totals are computed by hand from Expenses' SeedData (see ExpensesDbContext.cs):
    // Sales = 42.50 + 1200 + 6800 = 8042.50; Finance has no seeded expenses this period.
    // This is a fixture standing in for a real projection, not a real ETL pipeline — a
    // production system would populate this table from an event stream (e.g. an
    // "expense decided" event) or a scheduled job reading the Expenses database, kept in
    // sync as expenses change.
    public static async Task EnsureSeededAsync(ReportingDbContext db, TimeProvider timeProvider)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.DepartmentSpendSummaries.AnyAsync())
        {
            return;
        }

        var period = timeProvider.GetUtcNow().ToString("yyyy-MM");

        db.DepartmentSpendSummaries.AddRange(
            new DepartmentSpendSummary { Id = Guid.NewGuid(), Department = "Sales", Period = period,
                                          TotalAmount = 8042.50m, Currency = "USD" },
            new DepartmentSpendSummary { Id = Guid.NewGuid(), Department = "Finance", Period = period,
                                          TotalAmount = 0m, Currency = "USD" });

        await db.SaveChangesAsync();
    }
}
