using Meridian.DataAccess.Expenses;
using Meridian.DataAccess.Models;
using Meridian.Services;
using Meridian.Services.DTOs;

namespace Meridian.UnitTests.Services;

public class ExpenseServiceTests
{
    private const string OwnerUserId = "u-emma";
    private const string Department = "Sales";

    private static CallerContext BuildCaller(bool isFinance = false, bool isManager = false, string? department = Department) =>
        new(OwnerUserId, department, isFinance, isManager);

    private static Expense BuildExpense(
        string ownerUserId = OwnerUserId, string department = Department,
        ExpenseStatus status = ExpenseStatus.Submitted) => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = ownerUserId,
        Department = department,
        Amount = 100m,
        Category = "Meals",
        Status = status
    };

    [Fact]
    public async Task GetVisibleExpensesAsync_ReturnsEveryExpense_ForFinanceCaller()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildExpense(ownerUserId: "u-emma"), BuildExpense(ownerUserId: "u-mateo")]);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.GetVisibleExpensesAsync(BuildCaller(isFinance: true), CancellationToken.None);

        result.Should().HaveCount(2);
        repository.Verify(r => r.GetByOwnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetVisibleExpensesAsync_ReturnsOnlyOwnedExpenses_ForNonFinanceCaller()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.GetByOwnerAsync(OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildExpense()]);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.GetVisibleExpensesAsync(BuildCaller(), CancellationToken.None);

        result.Should().ContainSingle();
        repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.GetByDepartmentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetVisibleExpensesAsync_ReturnsDepartmentExpenses_ForManagerCaller()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.GetByDepartmentAsync(Department, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildExpense(ownerUserId: "u-emma"), BuildExpense(ownerUserId: "u-mateo")]);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.GetVisibleExpensesAsync(BuildCaller(isManager: true), CancellationToken.None);

        result.Should().HaveCount(2);
        repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.GetByOwnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetVisibleExpensesAsync_FallsBackToOwnedExpenses_ForManagerCallerWithNoDepartment()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.GetByOwnerAsync(OwnerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildExpense()]);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.GetVisibleExpensesAsync(BuildCaller(isManager: true, department: null), CancellationToken.None);

        result.Should().ContainSingle();
        repository.Verify(r => r.GetByDepartmentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenRepositoryFindsNothing()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expense?)null);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_AndDoesNotTouchRepository_WhenCallerHasNoDepartment()
    {
        var repository = new Mock<IExpenseRepository>(MockBehavior.Strict);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.CreateAsync(
            new CreateExpenseRequest(50m, "Meals"), BuildCaller(department: null), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_PersistsADraftExpense_OwnedByTheCaller()
    {
        Expense? added = null;
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()))
            .Callback<Expense, CancellationToken>((e, _) => added = e)
            .Returns(Task.CompletedTask);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.CreateAsync(
            new CreateExpenseRequest(75m, "Travel"), BuildCaller(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ExpenseStatus.Draft);
        result.OwnerUserId.Should().Be(OwnerUserId);
        result.Department.Should().Be(Department);
        result.Amount.Should().Be(75m);
        result.Category.Should().Be("Travel");
        added.Should().NotBeNull();
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_ReturnsNull_AndDoesNotReRead_WhenNoRowsWereUpdated()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.TrySubmitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.SubmitAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
        repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_ReturnsTheSubmittedExpense_WhenTheUpdateApplied()
    {
        var expenseId = Guid.NewGuid();
        var submitted = BuildExpense(status: ExpenseStatus.Submitted);
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.TrySubmitAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        repository.Setup(r => r.GetByIdAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submitted);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.SubmitAsync(expenseId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ExpenseStatus.Submitted);
    }

    [Fact]
    public async Task DecideAsync_ReturnsNull_AndDoesNotReRead_WhenNoRowsWereUpdated()
    {
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.TryDecideAsync(
                It.IsAny<Guid>(), It.IsAny<ExpenseStatus>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.DecideAsync(Guid.NewGuid(), ExpenseStatus.Approved, "u-priya", null, CancellationToken.None);

        result.Should().BeNull();
        repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DecideAsync_ReturnsTheDecidedExpense_WhenTheUpdateApplied()
    {
        var expenseId = Guid.NewGuid();
        var decided = BuildExpense(status: ExpenseStatus.Approved);
        var repository = new Mock<IExpenseRepository>();
        repository.Setup(r => r.TryDecideAsync(
                expenseId, ExpenseStatus.Approved, "u-priya", It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        repository.Setup(r => r.GetByIdAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(decided);
        var sut = new ExpenseService(repository.Object);

        var result = await sut.DecideAsync(expenseId, ExpenseStatus.Approved, "u-priya", null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ExpenseStatus.Approved);
    }
}
