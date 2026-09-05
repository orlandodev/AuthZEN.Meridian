using Meridian.DataAccess.Models;
using Meridian.DataAccess.Receipts;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;
using Meridian.Services.Mapping;

namespace Meridian.Services;

public sealed class ReceiptService(IReceiptRepository repository, IReceiptBlobStorage blobStorage) : IReceiptService
{
    // Caches the last Receipt fetched by id within this (per-request-scoped) instance,
    // so a GetMetadataByIdAsync call followed by a DownloadAsync call for the same id
    // — the download endpoint's authorize-then-fetch flow — only hits the database once.
    private Receipt? _lastFetched;

    private async Task<Receipt?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (_lastFetched?.Id != id)
        {
            _lastFetched = await repository.GetByIdAsync(id, ct);
        }
        return _lastFetched;
    }

    public async Task<IReadOnlyList<ReceiptDto>> GetForExpenseAsync(Guid expenseId, CallerContext caller, CancellationToken ct)
    {
        var receipts = await repository.GetByExpenseIdAsync(expenseId, ct);
        // Manager candidates are deliberately over-inclusive here (every
        // receipt on the expense, same as Finance) — Receipts.Api's
        // ReceiptVisibilityFilter narrows this down to a genuine ManagerOf
        // relationship via the PDP, the same way Expenses.Api's
        // ExpenseVisibilityFilter narrows ExpenseService's department-based
        // candidates. Callers that bypass that filter and call this method
        // directly get the unnarrowed (over-broad) set.
        var visible = (caller.IsFinance || caller.IsManager)
            ? receipts
            : receipts.Where(r => r.OwnerUserId == caller.UserId);
        return visible.Select(r => r.ToDto()).ToList();
    }

    public async Task<ReceiptDto?> GetMetadataByIdAsync(Guid id, CancellationToken ct) =>
        (await GetByIdAsync(id, ct))?.ToDto();

    public async Task<(Stream Content, string ContentType)?> DownloadAsync(Guid id, CancellationToken ct)
    {
        var receipt = await GetByIdAsync(id, ct);
        return receipt is null ? null : await blobStorage.DownloadAsync(receipt.BlobUri, ct);
    }

    public async Task<ReceiptDto> UploadAsync(
        Guid expenseId, Stream content, string fileName, string contentType, CallerContext caller, CancellationToken ct)
    {
        var receiptId = Guid.NewGuid();

        // Never trust the raw uploaded filename for the storage path — it can contain
        // path traversal characters. Path.GetFileName strips any directory component.
        var safeFileName = Path.GetFileName(fileName);
        var blobPath = $"{receiptId}/{safeFileName}";
        var blobUri = await blobStorage.UploadAsync(blobPath, content, contentType, ct);

        var receipt = new Receipt
        {
            Id = receiptId,
            ExpenseId = expenseId,
            OwnerUserId = caller.UserId,
            BlobUri = blobUri,
            ContentType = contentType,
            UploadedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(receipt, ct);
        await repository.SaveChangesAsync(ct);
        return receipt.ToDto();
    }
}
