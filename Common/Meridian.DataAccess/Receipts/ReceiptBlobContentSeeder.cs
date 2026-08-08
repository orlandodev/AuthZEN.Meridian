namespace Meridian.DataAccess.Receipts;

// The 2 Receipt rows themselves are seeded via ReceiptsDbContext's HasData
// migration with a placeholder BlobUri ("seed-pending") — HasData can't call
// the async blob storage upload, since the real BlobUri is only known at
// runtime (it embeds the Azurite/Storage host:port). This runs at service
// startup to upload the actual placeholder content and fill in the real
// BlobUri by fixed, known id. Guarded by that same "seed-pending" sentinel:
// once a row's BlobUri has been filled in, later startups skip it, so a
// container restart/redeploy doesn't re-upload placeholder content or
// re-issue blob writes indefinitely.
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
