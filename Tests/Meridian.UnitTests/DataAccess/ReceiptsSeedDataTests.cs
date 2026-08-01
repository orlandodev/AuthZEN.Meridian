using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class ReceiptsSeedDataTests
{
    private static ReceiptsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ReceiptsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Mock<IReceiptBlobStorage> CreateBlobStorage()
    {
        var mock = new Mock<IReceiptBlobStorage>();
        mock.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blobs.test/receipts/seed");
        return mock;
    }

    [Fact]
    public async Task EnsureSeededAsync_SeedsTwoReceipts_AndWritesAPlaceholderBlobForEach_WhenDatabaseIsEmpty()
    {
        using var db = CreateContext();
        var blobStorage = CreateBlobStorage();

        await ReceiptsSeedData.EnsureSeededAsync(db, blobStorage.Object);

        var receipts = await db.Receipts.ToListAsync();
        receipts.Should().HaveCount(2);
        receipts.Should().OnlyContain(r => r.Id != Guid.Empty);
        receipts.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.BlobUri));
        blobStorage.Verify(
            b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotDuplicateSeed_WhenCalledTwice()
    {
        using var db = CreateContext();
        var blobStorage = CreateBlobStorage();
        await ReceiptsSeedData.EnsureSeededAsync(db, blobStorage.Object);

        await ReceiptsSeedData.EnsureSeededAsync(db, blobStorage.Object);

        (await db.Receipts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotSeed_OrTouchBlobStorage_WhenDatabaseAlreadyHasData()
    {
        using var db = CreateContext();
        db.Receipts.Add(new Receipt
        {
            Id = Guid.NewGuid(),
            ExpenseId = Guid.NewGuid(),
            OwnerUserId = "u-existing",
            BlobUri = "https://blobs.test/receipts/existing",
            ContentType = "text/plain"
        });
        await db.SaveChangesAsync();
        var blobStorage = new Mock<IReceiptBlobStorage>(MockBehavior.Strict);

        await ReceiptsSeedData.EnsureSeededAsync(db, blobStorage.Object);

        (await db.Receipts.CountAsync()).Should().Be(1);
    }
}
