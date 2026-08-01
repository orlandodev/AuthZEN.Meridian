using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess;

public class ReceiptsDbContext(DbContextOptions<ReceiptsDbContext> options) : DbContext(options)
{
    public DbSet<Receipt> Receipts => Set<Receipt>();
}

public static class ReceiptsSeedData
{
    // Owner ids match the sub values of the Duende test users (see IdentityServer/TestUsers.cs) —
    // same convention as ExpensesSeedData. ExpenseId values below are fixed illustrative GUIDs,
    // NOT foreign keys into Expenses.Api's database: Receipts.Api and Expenses.Api are separate
    // services/databases with no referential integrity between them, and Expenses' own seed data
    // regenerates random Guids on every run. These rows exist to demonstrate ownership/authorization
    // behavior, not to model a real cross-service relationship.
    public static async Task EnsureSeededAsync(ReceiptsDbContext db, IReceiptBlobStorage blobStorage, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (await db.Receipts.AnyAsync(ct))
        {
            return;
        }

        var seedReceipts = new[]
        {
            new Receipt
            {
                Id = Guid.NewGuid(),
                ExpenseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                OwnerUserId = "u-emma",
                ContentType = "text/plain"
            },
            new Receipt
            {
                Id = Guid.NewGuid(),
                ExpenseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                OwnerUserId = "u-mateo",
                ContentType = "text/plain"
            }
        };

        foreach (var receipt in seedReceipts)
        {
            var blobPath = $"{receipt.Id}/receipt.txt";
            var placeholder = "Seeded placeholder receipt — replace by uploading a real file."u8.ToArray();
            using var content = new MemoryStream(placeholder);
            receipt.BlobUri = await blobStorage.UploadAsync(blobPath, content, receipt.ContentType, ct);
        }

        db.Receipts.AddRange(seedReceipts);
        await db.SaveChangesAsync(ct);
    }
}
