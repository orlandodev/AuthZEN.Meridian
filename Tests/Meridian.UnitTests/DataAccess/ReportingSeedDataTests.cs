using Meridian.DataAccess.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

// Department spend summaries are seeded via ReportingDbContext's HasData
// with a fixed Period ("2025-01") rather than the current month — see
// ReportingDbContext.OnModelCreating for why HasData needs a static value.
public class ReportingSeedDataTests
{
    private static ReportingDbContext CreateSeededContext()
    {
        var db = new ReportingDbContext(new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Seed_HasTwoDepartmentSummaries()
    {
        using var db = CreateSeededContext();

        var summaries = await db.DepartmentSpendSummaries.ToListAsync();

        summaries.Should().HaveCount(2);
        summaries.Should().OnlyContain(s => s.Id != Guid.Empty);
        summaries.Should().OnlyContain(s => s.Period == "2025-01");
    }

    [Fact]
    public async Task Seed_DepartmentsMatchTestUsers()
    {
        using var db = CreateSeededContext();

        var departments = (await db.DepartmentSpendSummaries.ToListAsync()).Select(s => s.Department);

        departments.Should().BeEquivalentTo(["Sales", "Finance"]);
    }
}
