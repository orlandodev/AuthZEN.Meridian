using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess;

public class ExpensesDbContext(DbContextOptions<ExpensesDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
}

public static class ExpensesSeedData
{
    // Owner ids match the sub values of the Duende test users (see IdentityServer/TestUsers.cs).
    public static async Task EnsureSeededAsync(ExpensesDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Expenses.AnyAsync())
        {
            return;
        }

        db.Expenses.AddRange(
            new Expense { Id = Guid.NewGuid(), OwnerUserId = "u-emma",  Department = "Sales",
                          Amount = 42.50m, Category = "Meals", Status = ExpenseStatus.Submitted },
            new Expense { Id = Guid.NewGuid(), OwnerUserId = "u-emma",  Department = "Sales",
                          Amount = 1200m, Category = "Travel", Status = ExpenseStatus.Draft },
            new Expense { Id = Guid.NewGuid(), OwnerUserId = "u-mateo", Department = "Sales",
                          Amount = 6800m, Category = "Conference", Status = ExpenseStatus.Submitted });

        await db.SaveChangesAsync();
    }
}
