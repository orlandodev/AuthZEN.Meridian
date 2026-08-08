using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess.Reporting;

public class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<DepartmentSpendSummary> DepartmentSpendSummaries => Set<DepartmentSpendSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // These totals are computed by hand from Expenses' seed data (see
        // ExpensesDbContext.cs): Sales = 42.50 + 1200 + 6800 = 8042.50;
        // Finance has no seeded expenses this period. This is a fixture
        // standing in for a real projection, not a real ETL pipeline — a
        // production system would populate this table from an event stream
        // or a scheduled job reading the Expenses database.
        //
        // Period is a fixed literal ("2025-01"), not TimeProvider.UtcNow's
        // current month: HasData is baked into the compiled migration at
        // `migrations add` time, so "the current month" isn't a valid seed
        // value — it would bake one fixed month into the migration while
        // OnModelCreating (which also runs at every app startup) keeps
        // computing a *different* "current month," which EF would see as a
        // spurious pending model change.
        modelBuilder.Entity<DepartmentSpendSummary>().HasData(
            new DepartmentSpendSummary
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
                Department = "Sales",
                Period = "2025-01",
                TotalAmount = 8042.50m,
                Currency = "USD"
            },
            new DepartmentSpendSummary
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000002"),
                Department = "Finance",
                Period = "2025-01",
                TotalAmount = 0m,
                Currency = "USD"
            });
    }
}
