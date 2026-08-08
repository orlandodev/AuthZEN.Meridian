using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess.Expenses;

public class ExpensesDbContext(DbContextOptions<ExpensesDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every value below is a fixed literal, including CreatedAt — HasData
        // is baked into the compiled migration (and the model snapshot used
        // to diff future migrations) at `migrations add` time, not executed
        // at app startup. Expense.CreatedAt defaults to DateTimeOffset.UtcNow
        // in the model class; leaving that default in place here would bake
        // one fixed "now" into the migration while OnModelCreating keeps
        // re-evaluating a *different* "now" on every subsequent app start,
        // which EF would see as a spurious pending model change. Owner ids
        // match the sub values of the Duende test users (see
        // Identity/Meridian.IdentityServer/TestUsers.cs).
        modelBuilder.Entity<Expense>().HasData(
            new Expense
            {
                Id = Guid.Parse("e0000000-0000-0000-0000-000000000001"),
                OwnerUserId = "u-emma",
                Department = "Sales",
                Amount = 42.50m,
                Currency = "USD",
                Category = "Meals",
                Status = ExpenseStatus.Submitted,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero)
            },
            new Expense
            {
                Id = Guid.Parse("e0000000-0000-0000-0000-000000000002"),
                OwnerUserId = "u-emma",
                Department = "Sales",
                Amount = 1200m,
                Currency = "USD",
                Category = "Travel",
                Status = ExpenseStatus.Draft,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero)
            },
            new Expense
            {
                Id = Guid.Parse("e0000000-0000-0000-0000-000000000003"),
                OwnerUserId = "u-mateo",
                Department = "Sales",
                Amount = 6800m,
                Currency = "USD",
                Category = "Conference",
                Status = ExpenseStatus.Submitted,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero)
            });
    }
}
