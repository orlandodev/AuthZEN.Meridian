using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class ExpenseRepositoryTests
{
    private static ExpensesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Expense NewExpense(
        string ownerUserId = "u-emma", string department = "Sales",
        decimal amount = 100m, ExpenseStatus status = ExpenseStatus.Submitted) => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = ownerUserId,
        Department = department,
        Amount = amount,
        Category = "Meals",
        Status = status
    };

    [Fact]
    public async Task GetAllAsync_ReturnsEveryExpense_RegardlessOfOwner()
    {
        using var db = CreateContext();
        db.Expenses.AddRange(NewExpense(ownerUserId: "u-emma"), NewExpense(ownerUserId: "u-mateo"));
        await db.SaveChangesAsync();
        var sut = new ExpenseRepository(db);

        var result = await sut.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByOwnerAsync_ReturnsOnlyThatOwnersExpenses()
    {
        using var db = CreateContext();
        var mine = NewExpense(ownerUserId: "u-emma");
        db.Expenses.AddRange(mine, NewExpense(ownerUserId: "u-mateo"));
        await db.SaveChangesAsync();
        var sut = new ExpenseRepository(db);

        var result = await sut.GetByOwnerAsync("u-emma", CancellationToken.None);

        result.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task GetByDepartmentAsync_ReturnsOnlyThatDepartmentsExpenses()
    {
        using var db = CreateContext();
        var sales = NewExpense(ownerUserId: "u-emma", department: "Sales");
        db.Expenses.AddRange(sales, NewExpense(ownerUserId: "u-priya", department: "Finance"));
        await db.SaveChangesAsync();
        var sut = new ExpenseRepository(db);

        var result = await sut.GetByDepartmentAsync("Sales", CancellationToken.None);

        result.Should().ContainSingle().Which.Id.Should().Be(sales.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenExpenseDoesNotExist()
    {
        using var db = CreateContext();
        var sut = new ExpenseRepository(db);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_ThenSaveChangesAsync_PersistsTheExpense()
    {
        using var db = CreateContext();
        var sut = new ExpenseRepository(db);
        var expense = NewExpense();

        await sut.AddAsync(expense, CancellationToken.None);
        await sut.SaveChangesAsync(CancellationToken.None);

        (await sut.GetByIdAsync(expense.Id, CancellationToken.None)).Should().NotBeNull();
    }

    // TryDecideAsync uses EF Core's ExecuteUpdateAsync, which the InMemory
    // provider used here does not implement (it throws InvalidOperationException).
    // That's a real relational feature only Npgsql (the production provider) can
    // exercise, so its 0-row/1-row contract is covered against a mocked
    // IExpenseRepository in ExpenseServiceTests instead.
}
