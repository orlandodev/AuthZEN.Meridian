using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess;

public sealed class ExpenseRepository(ExpensesDbContext db) : IExpenseRepository
{
    public Task<List<Expense>> GetAllAsync(CancellationToken ct) =>
        db.Expenses.AsNoTracking().ToListAsync(ct);

    public Task<List<Expense>> GetByOwnerAsync(string ownerUserId, CancellationToken ct) =>
        db.Expenses.AsNoTracking().Where(e => e.OwnerUserId == ownerUserId).ToListAsync(ct);

    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(Expense expense, CancellationToken ct) =>
        await db.Expenses.AddAsync(expense, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public Task<int> TryDecideAsync(Guid id, ExpenseStatus decision, string deciderUserId, DateTimeOffset decidedAt, CancellationToken ct) =>
        db.Expenses
            .Where(e => e.Id == id && e.Status == ExpenseStatus.Submitted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, decision)
                .SetProperty(e => e.ApproverUserId, deciderUserId)
                .SetProperty(e => e.DecidedAt, decidedAt), ct);
}
