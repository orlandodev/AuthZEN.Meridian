using Microsoft.EntityFrameworkCore.Design;

namespace Meridian.DataAccess.Expenses;

public sealed class ExpensesDbContextFactory : IDesignTimeDbContextFactory<ExpensesDbContext>
{
    public ExpensesDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Build<ExpensesDbContext>("expensesdb"));
}
