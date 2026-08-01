using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Meridian.Services;

namespace Meridian.UnitTests.Services;

public class ReportingServiceTests
{
    private const string Department = "Sales";

    private static CallerContext BuildCaller(bool isFinance = false, bool isManager = false, string? department = Department) =>
        new("u-nadia", department, isFinance, isManager);

    private static DepartmentSpendSummary BuildSummary(string department = Department, decimal totalAmount = 100m) => new()
    {
        Id = Guid.NewGuid(),
        Department = department,
        Period = "2026-07",
        TotalAmount = totalAmount,
        Currency = "USD"
    };

    [Fact]
    public async Task GetDepartmentSpendAsync_ReturnsEverySummary_ForFinanceCaller()
    {
        var repository = new Mock<IReportingRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildSummary(department: "Sales"), BuildSummary(department: "Finance")]);
        var sut = new ReportingService(repository.Object);

        var result = await sut.GetDepartmentSpendAsync(BuildCaller(isFinance: true), CancellationToken.None);

        result.Should().HaveCount(2);
        repository.Verify(r => r.GetByDepartmentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDepartmentSpendAsync_ReturnsOnlyOwnDepartment_ForManagerCaller()
    {
        var repository = new Mock<IReportingRepository>();
        repository.Setup(r => r.GetByDepartmentAsync(Department, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildSummary()]);
        var sut = new ReportingService(repository.Object);

        var result = await sut.GetDepartmentSpendAsync(BuildCaller(isManager: true), CancellationToken.None);

        result.Should().ContainSingle();
        repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDepartmentSpendAsync_QueriesEmptyDepartment_WhenNonFinanceCallerHasNoDepartmentClaim()
    {
        var repository = new Mock<IReportingRepository>();
        repository.Setup(r => r.GetByDepartmentAsync(string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = new ReportingService(repository.Object);

        var result = await sut.GetDepartmentSpendAsync(BuildCaller(isManager: true, department: null), CancellationToken.None);

        result.Should().BeEmpty();
        repository.Verify(r => r.GetByDepartmentAsync(string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }
}
