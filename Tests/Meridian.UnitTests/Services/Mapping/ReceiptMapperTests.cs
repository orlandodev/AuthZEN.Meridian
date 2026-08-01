using Meridian.DataAccess.Models;
using Meridian.Services.Mapping;

namespace Meridian.UnitTests.Services.Mapping;

public class ReceiptMapperTests
{
    [Fact]
    public void ToDto_CopiesEveryField_AndDerivesFileNameFromBlobUri()
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ExpenseId = Guid.NewGuid(),
            OwnerUserId = "u-emma",
            BlobUri = $"https://blobs.test/receipts/{Guid.NewGuid()}/lunch-receipt.jpg",
            ContentType = "image/jpeg",
            UploadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var dto = receipt.ToDto();

        dto.Id.Should().Be(receipt.Id);
        dto.ExpenseId.Should().Be(receipt.ExpenseId);
        dto.OwnerUserId.Should().Be(receipt.OwnerUserId);
        dto.ContentType.Should().Be(receipt.ContentType);
        dto.UploadedAt.Should().Be(receipt.UploadedAt);
        dto.FileName.Should().Be("lunch-receipt.jpg");
    }

    [Fact]
    public void ToDto_UnescapesUrlEncodedCharacters_InTheFileName()
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ExpenseId = Guid.NewGuid(),
            OwnerUserId = "u-emma",
            BlobUri = $"https://blobs.test/receipts/{Guid.NewGuid()}/my%20receipt.jpg",
            ContentType = "image/jpeg",
            UploadedAt = DateTimeOffset.UtcNow
        };

        var dto = receipt.ToDto();

        dto.FileName.Should().Be("my receipt.jpg");
    }
}
