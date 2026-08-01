using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Meridian.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class ReportingSeedDataTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));

    private static ReportingDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task EnsureSeededAsync_SeedsTwoDepartmentSummaries_WhenDatabaseIsEmpty()
    {
        using var db = CreateContext();

        await ReportingSeedData.EnsureSeededAsync(db, Clock);

        var summaries = await db.DepartmentSpendSummaries.ToListAsync();
        summaries.Should().HaveCount(2);
        summaries.Should().OnlyContain(s => s.Id != Guid.Empty);
        summaries.Should().OnlyContain(s => s.Period == "2026-07");
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotDuplicateSeed_WhenCalledTwice()
    {
        using var db = CreateContext();
        await ReportingSeedData.EnsureSeededAsync(db, Clock);

        await ReportingSeedData.EnsureSeededAsync(db, Clock);

        (await db.DepartmentSpendSummaries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotSeed_WhenDatabaseAlreadyHasData()
    {
        using var db = CreateContext();
        db.DepartmentSpendSummaries.Add(new DepartmentSpendSummary
        {
            Id = Guid.NewGuid(),
            Department = "Ops",
            Period = "2026-01",
            TotalAmount = 10m,
            Currency = "USD"
        });
        await db.SaveChangesAsync();

        await ReportingSeedData.EnsureSeededAsync(db, Clock);

        (await db.DepartmentSpendSummaries.CountAsync()).Should().Be(1);
    }
}
