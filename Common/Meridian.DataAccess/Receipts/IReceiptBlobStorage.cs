namespace Meridian.DataAccess.Receipts;

public interface IReceiptBlobStorage
{
    // blobPath is the sanitized "{receiptId}/{safeFileName}" key. Returns the full blob URI.
    Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct);

    // Returns null if the blob no longer exists.
    Task<(Stream Content, string ContentType)?> DownloadAsync(string blobUri, CancellationToken ct);
}
