using Meridian.DataAccess.Models;
using Meridian.DataAccess.Receipts;
using Meridian.Services;

namespace Meridian.UnitTests.Services;

public class ReceiptServiceTests
{
    private const string OwnerUserId = "u-emma";

    private static CallerContext BuildCaller(bool isFinance = false, bool isManager = false) =>
        new(OwnerUserId, "Sales", isFinance, isManager);

    private static Receipt BuildReceipt(
        Guid expenseId, string ownerUserId = OwnerUserId, string blobUri = "https://blobs.test/receipts/x/receipt.jpg") => new()
    {
        Id = Guid.NewGuid(),
        ExpenseId = expenseId,
        OwnerUserId = ownerUserId,
        BlobUri = blobUri,
        ContentType = "image/jpeg"
    };

    [Fact]
    public async Task GetForExpenseAsync_ReturnsEveryReceipt_ForFinanceCaller()
    {
        var expenseId = Guid.NewGuid();
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.GetByExpenseIdAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildReceipt(expenseId, "u-emma"), BuildReceipt(expenseId, "u-mateo")]);
        var sut = new ReceiptService(repository.Object, Mock.Of<IReceiptBlobStorage>());

        var result = await sut.GetForExpenseAsync(expenseId, BuildCaller(isFinance: true), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetForExpenseAsync_ReturnsOnlyOwnedReceipts_ForNonFinanceNonManagerCaller()
    {
        var expenseId = Guid.NewGuid();
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.GetByExpenseIdAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildReceipt(expenseId, "u-emma"), BuildReceipt(expenseId, "u-mateo")]);
        var sut = new ReceiptService(repository.Object, Mock.Of<IReceiptBlobStorage>());

        var result = await sut.GetForExpenseAsync(expenseId, BuildCaller(), CancellationToken.None);

        result.Should().ContainSingle().Which.OwnerUserId.Should().Be(OwnerUserId);
    }

    [Fact]
    public async Task GetForExpenseAsync_ReturnsEveryReceipt_ForManagerCaller()
    {
        // Deliberately over-inclusive, same as the Finance branch — Receipts.Api's
        // ReceiptVisibilityFilter is what narrows this down to a genuine ManagerOf
        // relationship via the PDP; this method's own job is just to not hide
        // candidates the filter still needs to see.
        var expenseId = Guid.NewGuid();
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.GetByExpenseIdAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildReceipt(expenseId, "u-emma"), BuildReceipt(expenseId, "u-mateo")]);
        var sut = new ReceiptService(repository.Object, Mock.Of<IReceiptBlobStorage>());

        var result = await sut.GetForExpenseAsync(expenseId, BuildCaller(isManager: true), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMetadataByIdAsync_ReturnsNull_WhenRepositoryFindsNothing()
    {
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt?)null);
        var sut = new ReceiptService(repository.Object, Mock.Of<IReceiptBlobStorage>());

        var result = await sut.GetMetadataByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNull_AndNeverTouchesBlobStorage_WhenReceiptDoesNotExist()
    {
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt?)null);
        var blobStorage = new Mock<IReceiptBlobStorage>(MockBehavior.Strict);
        var sut = new ReceiptService(repository.Object, blobStorage.Object);

        var result = await sut.DownloadAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNull_WhenBlobNoLongerExists()
    {
        var receipt = BuildReceipt(Guid.NewGuid());
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.GetByIdAsync(receipt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        var blobStorage = new Mock<IReceiptBlobStorage>();
        blobStorage.Setup(b => b.DownloadAsync(receipt.BlobUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Stream Content, string ContentType)?)null);
        var sut = new ReceiptService(repository.Object, blobStorage.Object);

        var result = await sut.DownloadAsync(receipt.Id, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UploadAsync_PersistsAReceipt_OwnedByTheCaller()
    {
        Receipt? added = null;
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.AddAsync(It.IsAny<Receipt>(), It.IsAny<CancellationToken>()))
            .Callback<Receipt, CancellationToken>((r, _) => added = r)
            .Returns(Task.CompletedTask);
        var blobStorage = new Mock<IReceiptBlobStorage>();
        blobStorage.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blobs.test/receipts/uploaded");
        var sut = new ReceiptService(repository.Object, blobStorage.Object);
        var expenseId = Guid.NewGuid();

        using var content = new MemoryStream([1, 2, 3]);
        var result = await sut.UploadAsync(expenseId, content, "lunch.jpg", "image/jpeg", BuildCaller(), CancellationToken.None);

        result.OwnerUserId.Should().Be(OwnerUserId);
        result.ExpenseId.Should().Be(expenseId);
        added.Should().NotBeNull();
        added!.OwnerUserId.Should().Be(OwnerUserId);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_SanitizesAPathTraversalFileName_BeforeUploadingToBlobStorage()
    {
        string? uploadedPath = null;
        var repository = new Mock<IReceiptRepository>();
        repository.Setup(r => r.AddAsync(It.IsAny<Receipt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var blobStorage = new Mock<IReceiptBlobStorage>();
        blobStorage.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((path, _, _, _) => uploadedPath = path)
            .ReturnsAsync("https://blobs.test/receipts/uploaded");
        var sut = new ReceiptService(repository.Object, blobStorage.Object);

        using var content = new MemoryStream([1, 2, 3]);
        await sut.UploadAsync(Guid.NewGuid(), content, "../../evil.txt", "text/plain", BuildCaller(), CancellationToken.None);

        uploadedPath.Should().NotBeNull();
        uploadedPath.Should().EndWith("/evil.txt");
        uploadedPath.Should().NotContain("..");
    }
}
