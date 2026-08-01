using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

public class ReceiptRepositoryTests
{
    private static ReceiptsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ReceiptsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Receipt NewReceipt(Guid expenseId, string ownerUserId = "u-emma") => new()
    {
        Id = Guid.NewGuid(),
        ExpenseId = expenseId,
        OwnerUserId = ownerUserId,
        BlobUri = $"https://blobs.test/receipts/{Guid.NewGuid()}/receipt.jpg",
        ContentType = "image/jpeg"
    };

    [Fact]
    public async Task GetByExpenseIdAsync_ReturnsEveryReceiptForThatExpense_RegardlessOfOwner()
    {
        using var db = CreateContext();
        var expenseId = Guid.NewGuid();
        db.Receipts.AddRange(
            NewReceipt(expenseId, ownerUserId: "u-emma"),
            NewReceipt(expenseId, ownerUserId: "u-mateo"),
            NewReceipt(Guid.NewGuid(), ownerUserId: "u-emma")); // different expense
        await db.SaveChangesAsync();
        var sut = new ReceiptRepository(db);

        var result = await sut.GetByExpenseIdAsync(expenseId, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenReceiptDoesNotExist()
    {
        using var db = CreateContext();
        var sut = new ReceiptRepository(db);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_ThenSaveChangesAsync_PersistsTheReceipt()
    {
        using var db = CreateContext();
        var sut = new ReceiptRepository(db);
        var receipt = NewReceipt(Guid.NewGuid());

        await sut.AddAsync(receipt, CancellationToken.None);
        await sut.SaveChangesAsync(CancellationToken.None);

        (await sut.GetByIdAsync(receipt.Id, CancellationToken.None)).Should().NotBeNull();
    }
}
