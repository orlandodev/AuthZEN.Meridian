using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess.Reporting;

public class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<DepartmentSpendSummary> DepartmentSpendSummaries => Set<DepartmentSpendSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Totals are computed by hand from Expenses' seed data (Sales =
        // 42.50 + 1200 + 6800 = 8042.50; Finance has none) — a fixture
        // standing in for a real projection a production system would
        // populate from an event stream or scheduled job.
        //
        // Period is a fixed literal ("2025-01"), not the current month: a
        // live value would drift from what OnModelCreating recomputes on
        // every startup, which EF sees as a spurious model change.
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
