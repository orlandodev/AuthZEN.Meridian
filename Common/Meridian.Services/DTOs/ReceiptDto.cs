namespace Meridian.Services.DTOs;

// BlobUri is deliberately excluded — it's an internal storage detail, not something
// the API should leak to clients. FileName is derived from it instead (see ReceiptMapper).
public sealed record ReceiptDto(
    Guid Id,
    Guid ExpenseId,
    string OwnerUserId,
    string FileName,
    string ContentType,
    DateTimeOffset UploadedAt);
