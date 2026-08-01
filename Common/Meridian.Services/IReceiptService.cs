using Meridian.Services.DTOs;

namespace Meridian.Services;

public interface IReceiptService
{
    // Finance sees every receipt for the expense; everyone else sees only their own.
    Task<IReadOnlyList<ReceiptDto>> GetForExpenseAsync(Guid expenseId, CallerContext caller, CancellationToken ct);

    Task<ReceiptDto?> GetMetadataByIdAsync(Guid id, CancellationToken ct);

    // Unfiltered by caller — used only to establish the resource-based ownership
    // check on upload (see ReceiptEndpoints.MapPost). Not for display.
    Task<ReceiptDto?> GetAnyMetadataForExpenseAsync(Guid expenseId, CancellationToken ct);

    // Streams blob content back; null if the receipt or its blob no longer exists.
    Task<(Stream Content, string ContentType)?> DownloadAsync(Guid id, CancellationToken ct);

    // fileName is the caller-supplied original name; it's sanitized before it's used
    // as part of the storage path.
    Task<ReceiptDto> UploadAsync(
        Guid expenseId, Stream content, string fileName, string contentType, CallerContext caller, CancellationToken ct);
}
