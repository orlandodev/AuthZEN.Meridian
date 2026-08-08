using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Meridian.DataAccess.Receipts;

namespace Meridian.DataAccess;

// Registered as a singleton: BlobServiceClient is itself safe to share, and the
// lazily-created container handle below only needs to happen once per process.
public sealed class AzureBlobReceiptStorage(BlobServiceClient blobServiceClient) : IReceiptBlobStorage
{
    private const string ContainerName = "receipts";
    private readonly Lazy<Task> _containerReady = new(() =>
        blobServiceClient.GetBlobContainerClient(ContainerName).CreateIfNotExistsAsync());

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
    {
        await _containerReady.Value;

        var container = blobServiceClient.GetBlobContainerClient(ContainerName);
        var blob = container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        return blob.Uri.ToString();
    }

    public async Task<(Stream Content, string ContentType)?> DownloadAsync(string blobUri, CancellationToken ct)
    {
        var uriBuilder = new BlobUriBuilder(new Uri(blobUri));
        var container = blobServiceClient.GetBlobContainerClient(uriBuilder.BlobContainerName);
        var blob = container.GetBlobClient(uriBuilder.BlobName);

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return (download.Value.Content, download.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
