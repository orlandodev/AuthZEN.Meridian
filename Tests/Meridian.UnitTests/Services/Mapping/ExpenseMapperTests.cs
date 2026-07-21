using Meridian.DataAccess.Models;
using Meridian.Services.Mapping;

namespace Meridian.UnitTests.Services.Mapping;

public class ExpenseMapperTests
{
    [Fact]
    public void ToDto_CopiesEveryFieldFromTheEntity()
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "u-emma",
            Department = "Sales",
            Amount = 42.50m,
            Currency = "USD",
            Category = "Meals",
            Status = ExpenseStatus.Approved,
            ApproverUserId = "u-priya",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DecidedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };

        var dto = expense.ToDto();

        dto.Id.Should().Be(expense.Id);
        dto.OwnerUserId.Should().Be(expense.OwnerUserId);
        dto.Department.Should().Be(expense.Department);
        dto.Amount.Should().Be(expense.Amount);
        dto.Currency.Should().Be(expense.Currency);
        dto.Category.Should().Be(expense.Category);
        dto.Status.Should().Be(expense.Status);
        dto.ApproverUserId.Should().Be(expense.ApproverUserId);
        dto.CreatedAt.Should().Be(expense.CreatedAt);
        dto.DecidedAt.Should().Be(expense.DecidedAt);
    }

    [Fact]
    public void ToDto_MapsNullApproverAndDecidedAt_ForAnUndecidedExpense()
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "u-emma",
            Department = "Sales",
            Amount = 10m,
            Category = "Meals",
            Status = ExpenseStatus.Draft
        };

        var dto = expense.ToDto();

        dto.ApproverUserId.Should().BeNull();
        dto.DecidedAt.Should().BeNull();
    }
}
