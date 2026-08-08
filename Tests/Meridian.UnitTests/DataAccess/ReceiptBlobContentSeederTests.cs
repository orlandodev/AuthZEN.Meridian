using Meridian.DataAccess.Receipts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class ReceiptBlobContentSeederTests
{
    private static ReceiptsDbContext CreateSeededContext()
    {
        var db = new ReceiptsDbContext(new DbContextOptionsBuilder<ReceiptsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static Mock<IReceiptBlobStorage> CreateBlobStorage()
    {
        var mock = new Mock<IReceiptBlobStorage>();
        mock.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blobs.test/receipts/seed");
        return mock;
    }

    [Fact]
    public async Task EnsureBlobContentAsync_UploadsAndUpdatesBlobUri_ForEachSeededReceipt()
    {
        using var db = CreateSeededContext();
        var blobStorage = CreateBlobStorage();

        await ReceiptBlobContentSeeder.EnsureBlobContentAsync(db, blobStorage.Object);

        var receipts = await db.Receipts.ToListAsync();
        receipts.Should().OnlyContain(r => r.BlobUri == "https://blobs.test/receipts/seed");
        blobStorage.Verify(
            b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EnsureBlobContentAsync_IsIdempotent_WhenRunTwice()
    {
        using var db = CreateSeededContext();
        var blobStorage = CreateBlobStorage();
        await ReceiptBlobContentSeeder.EnsureBlobContentAsync(db, blobStorage.Object);

        await ReceiptBlobContentSeeder.EnsureBlobContentAsync(db, blobStorage.Object);

        (await db.Receipts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task EnsureBlobContentAsync_DoesNotReuploadOrTouchBlobStorage_OnSecondRun()
    {
        // Regression: previously this ran unconditionally on every service
        // startup, re-uploading placeholder content indefinitely.
        using var db = CreateSeededContext();
        var blobStorage = CreateBlobStorage();
        await ReceiptBlobContentSeeder.EnsureBlobContentAsync(db, blobStorage.Object);
        blobStorage.Invocations.Clear();

        await ReceiptBlobContentSeeder.EnsureBlobContentAsync(db, blobStorage.Object);

        blobStorage.Verify(
            b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
