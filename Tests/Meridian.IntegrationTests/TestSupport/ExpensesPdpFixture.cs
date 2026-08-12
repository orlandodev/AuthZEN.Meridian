using Meridian.DataAccess.Expenses;
using Meridian.DataAccess.Models;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Meridian.IntegrationTests.TestSupport;

// Shared across every test in a test class (xUnit IClassFixture semantics):
// starts a real Postgres container, builds both in-process hosts against it,
// then seeds a handful of expenses dedicated to the mutating (approve/reject)
// tests so they can't step on each other regardless of test execution order.
// Expense.HasData's three seed rows (see ExpensesDbContext, applied
// automatically by Expenses.Api's own startup migration) stay read-only in
// these tests — they're used for read/create scenarios only.
public sealed class ExpensesPdpFixture : IAsyncLifetime
{
    public static readonly Guid ApproveUnderLimitExpenseId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    public static readonly Guid ApproveOverLimitExpenseId = Guid.Parse("f0000000-0000-0000-0000-000000000002");
    public static readonly Guid FinanceApprovesAnyAmountExpenseId = Guid.Parse("f0000000-0000-0000-0000-000000000003");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    public PdpApiFactory Pdp { get; } = new();
    public ExpensesApiFactory Expenses { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Expenses = new ExpensesApiFactory(Pdp, _postgres.GetConnectionString());

        // Accessing Services forces the host to build, running Program.cs's
        // own MigrateOrEnsureCreatedAsync — real migrations and HasData's
        // three rows, applied exactly as they would be in production.
        using var scope = Expenses.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpensesDbContext>();

        db.Expenses.AddRange(
            new Expense
            {
                Id = ApproveUnderLimitExpenseId,
                OwnerUserId = "u-emma",
                Department = "Sales",
                Amount = 100m,
                Currency = "USD",
                Category = "Meals",
                Status = ExpenseStatus.Submitted
            },
            new Expense
            {
                Id = ApproveOverLimitExpenseId,
                OwnerUserId = "u-emma",
                Department = "Sales",
                Amount = 6800m,
                Currency = "USD",
                Category = "Conference",
                Status = ExpenseStatus.Submitted
            },
            new Expense
            {
                Id = FinanceApprovesAnyAmountExpenseId,
                OwnerUserId = "u-emma",
                Department = "Sales",
                Amount = 9999m,
                Currency = "USD",
                Category = "Equipment",
                Status = ExpenseStatus.Submitted
            });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Expenses.Dispose();
        Pdp.Dispose();
        await _postgres.DisposeAsync();
    }
}
