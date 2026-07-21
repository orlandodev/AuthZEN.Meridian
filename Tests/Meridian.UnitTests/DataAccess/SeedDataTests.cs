using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class SeedDataTests
{
    private static ExpensesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task EnsureSeededAsync_SeedsThreeExpenses_WhenDatabaseIsEmpty()
    {
        using var db = CreateContext();

        await SeedData.EnsureSeededAsync(db);

        var expenses = await db.Expenses.ToListAsync();
        expenses.Should().HaveCount(3);
        expenses.Should().OnlyContain(e => e.Id != Guid.Empty);
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotDuplicateSeed_WhenCalledTwice()
    {
        using var db = CreateContext();
        await SeedData.EnsureSeededAsync(db);

        await SeedData.EnsureSeededAsync(db);

        (await db.Expenses.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotSeed_WhenDatabaseAlreadyHasData()
    {
        using var db = CreateContext();
        db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "u-existing",
            Department = "Ops",
            Amount = 10m,
            Category = "Existing",
            Status = ExpenseStatus.Draft
        });
        await db.SaveChangesAsync();

        await SeedData.EnsureSeededAsync(db);

        (await db.Expenses.CountAsync()).Should().Be(1);
    }
}
