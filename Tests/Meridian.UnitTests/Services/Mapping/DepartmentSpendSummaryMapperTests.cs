using Meridian.DataAccess.Models;
using Meridian.Services.Mapping;

namespace Meridian.UnitTests.Services.Mapping;

public class DepartmentSpendSummaryMapperTests
{
    [Fact]
    public void ToDto_CopiesEveryFieldFromTheEntity()
    {
        var summary = new DepartmentSpendSummary
        {
            Id = Guid.NewGuid(),
            Department = "Sales",
            Period = "2026-07",
            TotalAmount = 8042.50m,
            Currency = "USD"
        };

        var dto = summary.ToDto();

        dto.Id.Should().Be(summary.Id);
        dto.Department.Should().Be(summary.Department);
        dto.Period.Should().Be(summary.Period);
        dto.TotalAmount.Should().Be(summary.TotalAmount);
        dto.Currency.Should().Be(summary.Currency);
    }
}
