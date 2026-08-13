namespace Meridian.DataAccess.Receipts;

// ReceiptsDbContext's HasData seeds the 2 Receipt rows with a placeholder
// BlobUri ("seed-pending"), since HasData can't call the async blob upload.
// This runs at startup to upload the real placeholder content and fill in
// the real BlobUri by fixed id; the same sentinel makes it idempotent, so a
// restart doesn't re-upload.
public static class ReceiptBlobContentSeeder
{
    private const string PendingBlobUri = "seed-pending";

    private static readonly (Guid Id, string ContentType)[] Fixed =
    [
        (Guid.Parse("b0000000-0000-0000-0000-000000000001"), "text/plain"),
        (Guid.Parse("b0000000-0000-0000-0000-000000000002"), "text/plain"),
    ];

    public static async Task EnsureBlobContentAsync(
        ReceiptsDbContext db, IReceiptBlobStorage blobStorage, CancellationToken ct = default)
    {
        var changed = false;
        foreach (var (id, contentType) in Fixed)
        {
            var receipt = await db.Receipts.FindAsync([id], ct);
            if (receipt is null || receipt.BlobUri != PendingBlobUri)
            {
                continue;
            }

            var blobPath = $"{id}/receipt.txt";
            var placeholder = "Seeded placeholder receipt — replace by uploading a real file."u8.ToArray();
            using var content = new MemoryStream(placeholder);
            receipt.BlobUri = await blobStorage.UploadAsync(blobPath, content, contentType, ct);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
