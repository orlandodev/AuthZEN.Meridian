using Meridian.DataAccess.Models;

namespace Meridian.DataAccess;

public interface IReceiptRepository
{
    // Unfiltered by owner — the service layer applies the caller's visibility rule.
    Task<List<Receipt>> GetByExpenseIdAsync(Guid expenseId, CancellationToken ct);

    // No-tracking read, for responses that never get mutated.
    Task<Receipt?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(Receipt receipt, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
