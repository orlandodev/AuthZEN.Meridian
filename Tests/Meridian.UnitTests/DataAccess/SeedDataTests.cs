using Meridian.DataAccess.Expenses;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

// Expenses are seeded via ExpensesDbContext's OnModelCreating HasData. EF
// InMemory + EnsureCreated() applies HasData the same way Migrate() does
// against a real Postgres database, so this exercises the exact same rows.
public class SeedDataTests
{
    private static ExpensesDbContext CreateSeededContext()
    {
        var db = new ExpensesDbContext(new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Seed_HasThreeExpenses()
    {
        using var db = CreateSeededContext();

        var expenses = await db.Expenses.ToListAsync();

        expenses.Should().HaveCount(3);
        expenses.Should().OnlyContain(e => e.Id != Guid.Empty);
    }

    [Fact]
    public async Task Seed_OwnerIdsMatchTestUsers()
    {
        using var db = CreateSeededContext();

        var ownerIds = (await db.Expenses.ToListAsync()).Select(e => e.OwnerUserId).Distinct();

        ownerIds.Should().BeEquivalentTo(["u-emma", "u-mateo"]);
    }
}
