using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.Services.Mapping;

public static class ReceiptMapper
{
    public static ReceiptDto ToDto(this Receipt receipt) => new(
        receipt.Id,
        receipt.ExpenseId,
        receipt.OwnerUserId,
        ExtractFileName(receipt.BlobUri),
        receipt.ContentType,
        receipt.UploadedAt);

    // Blob path is stored as "{receiptId}/{safeFileName}" — the segment after the
    // receiptId prefix is the (sanitized) original filename.
    private static string ExtractFileName(string blobUri) =>
        Uri.UnescapeDataString(new Uri(blobUri).Segments[^1]);
}
