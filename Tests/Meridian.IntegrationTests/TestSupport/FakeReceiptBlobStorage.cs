using Meridian.DataAccess.Receipts;

namespace Meridian.IntegrationTests.TestSupport;

// Swapped in for AzureBlobReceiptStorage in ReceiptsApiFactory: blob storage
// is orthogonal to what these tests prove (the PDP round trip), but
// Program.cs's startup seeder (ReceiptBlobContentSeeder) needs *some* working
// IReceiptBlobStorage regardless of what's under test — this keeps content
// entirely in-process instead of standing up a real Azurite container.
internal sealed class FakeReceiptBlobStorage : IReceiptBlobStorage
{
    private readonly Dictionary<string, (byte[] Content, string ContentType)> _blobs = [];

    public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var uri = $"fake://{blobPath}";
        _blobs[uri] = (buffer.ToArray(), contentType);
        return Task.FromResult(uri);
    }

    public Task<(Stream Content, string ContentType)?> DownloadAsync(string blobUri, CancellationToken ct) =>
        Task.FromResult(_blobs.TryGetValue(blobUri, out var blob)
            ? ((Stream Content, string ContentType)?)(new MemoryStream(blob.Content), blob.ContentType)
            : null);
}
