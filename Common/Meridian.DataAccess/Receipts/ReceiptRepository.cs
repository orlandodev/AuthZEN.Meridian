using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess.Receipts;

public sealed class ReceiptRepository(ReceiptsDbContext db) : IReceiptRepository
{
    public Task<List<Receipt>> GetByExpenseIdAsync(Guid expenseId, CancellationToken ct) =>
        db.Receipts.AsNoTracking().Where(r => r.ExpenseId == expenseId).OrderBy(r => r.UploadedAt).ToListAsync(ct);

    public Task<Receipt?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Receipt receipt, CancellationToken ct) =>
        await db.Receipts.AddAsync(receipt, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
