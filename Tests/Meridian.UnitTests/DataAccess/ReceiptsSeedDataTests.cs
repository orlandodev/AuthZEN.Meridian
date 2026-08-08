using Meridian.DataAccess.Receipts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.DataAccess;

// The 2 Receipt rows are now seeded via ReceiptsDbContext's HasData (baked
// into the InitialCreate migration) with a fixed placeholder BlobUri —
// HasData can't call the async blob upload. ReceiptBlobContentSeeder runs at
// service startup to upload real placeholder content and fill in BlobUri;
// see ReceiptBlobContentSeederTests below for that half.
public class ReceiptsSeedDataTests
{
    private static ReceiptsDbContext CreateSeededContext()
    {
        var db = new ReceiptsDbContext(new DbContextOptionsBuilder<ReceiptsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Seed_HasTwoReceipts()
    {
        using var db = CreateSeededContext();

        var receipts = await db.Receipts.ToListAsync();

        receipts.Should().HaveCount(2);
        receipts.Should().OnlyContain(r => r.Id != Guid.Empty);
    }

    [Fact]
    public async Task Seed_OwnerIdsMatchTestUsers()
    {
        using var db = CreateSeededContext();

        var ownerIds = (await db.Receipts.ToListAsync()).Select(r => r.OwnerUserId).Distinct();

        ownerIds.Should().BeEquivalentTo(["u-emma", "u-mateo"]);
    }
}
