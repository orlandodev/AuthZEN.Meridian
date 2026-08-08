using Meridian.DataAccess.Models;

namespace Meridian.DataAccess.Expenses;

public interface IExpenseRepository
{
    Task<List<Expense>> GetAllAsync(CancellationToken ct);

    Task<List<Expense>> GetByOwnerAsync(string ownerUserId, CancellationToken ct);

    Task<List<Expense>> GetByDepartmentAsync(string department, CancellationToken ct);

    // No-tracking read, for responses that never get mutated.
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(Expense expense, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    // Atomically transitions the expense to decision only if it is still Submitted.
    // Returns the number of rows affected: 0 means the expense didn't exist, or was
    // no longer Submitted (already decided by a concurrent caller).
    Task<int> TryDecideAsync(Guid id, ExpenseStatus decision, string deciderUserId, DateTimeOffset decidedAt, CancellationToken ct);
}
