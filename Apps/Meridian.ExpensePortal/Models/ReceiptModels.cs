namespace Meridian.ExpensePortal.Models;

// Client-side mirror of Meridian.Services.DTOs.ReceiptDto. No blob bytes in this shape —
// those come from a separate download call (ReceiptsApiClient.DownloadReceiptAsync).
public sealed record ReceiptDto(
    Guid Id,
    Guid ExpenseId,
    string FileName,
    string ContentType,
    DateTimeOffset UploadedAt);
