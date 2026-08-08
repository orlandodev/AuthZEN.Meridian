using Meridian.DataAccess.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

// Department spend summaries are now seeded via ReportingDbContext's HasData
// (baked into the InitialCreate migration) with a fixed Period ("2025-01")
// rather than TimeProvider.UtcNow's current month — HasData needs
// compile-time-static values, so "the current month" is no longer a valid
// seed value. See ReportingDbContext.OnModelCreating for why.
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
