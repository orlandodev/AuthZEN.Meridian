using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class ReportingRepositoryTests
{
    private static ReportingDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DepartmentSpendSummary NewSummary(
        string department = "Sales", string period = "2026-07", decimal totalAmount = 100m) => new()
    {
        Id = Guid.NewGuid(),
        Department = department,
        Period = period,
        TotalAmount = totalAmount,
        Currency = "USD"
    };

    [Fact]
    public async Task GetAllAsync_ReturnsEverySummary_RegardlessOfDepartment()
    {
        using var db = CreateContext();
        db.DepartmentSpendSummaries.AddRange(NewSummary(department: "Sales"), NewSummary(department: "Finance"));
        await db.SaveChangesAsync();
        var sut = new ReportingRepository(db);

        var result = await sut.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByDepartmentAsync_ReturnsOnlyThatDepartmentsSummaries()
    {
        using var db = CreateContext();
        var sales = NewSummary(department: "Sales");
        db.DepartmentSpendSummaries.AddRange(sales, NewSummary(department: "Finance"));
        await db.SaveChangesAsync();
        var sut = new ReportingRepository(db);

        var result = await sut.GetByDepartmentAsync("Sales", CancellationToken.None);

        result.Should().ContainSingle().Which.Id.Should().Be(sales.Id);
    }

    [Fact]
    public async Task GetByDepartmentAsync_ReturnsEmpty_WhenDepartmentHasNoSummaries()
    {
        using var db = CreateContext();
        db.DepartmentSpendSummaries.Add(NewSummary(department: "Sales"));
        await db.SaveChangesAsync();
        var sut = new ReportingRepository(db);

        var result = await sut.GetByDepartmentAsync("Ops", CancellationToken.None);

        result.Should().BeEmpty();
    }
}
