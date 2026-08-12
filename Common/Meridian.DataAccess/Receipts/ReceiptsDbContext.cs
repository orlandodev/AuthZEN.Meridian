using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess.Receipts;

public class ReceiptsDbContext(DbContextOptions<ReceiptsDbContext> options) : DbContext(options)
{
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BlobUri is a placeholder ("seed-pending") since the real URI is
        // only known at runtime (it embeds the Azurite host:port). See
        // ReceiptBlobContentSeeder, run at startup, which fills it in by
        // these fixed ids. ExpenseId values are illustrative GUIDs, not real
        // foreign keys — Receipts.Api and Expenses.Api are separate
        // databases with no referential integrity between them.
        modelBuilder.Entity<Receipt>().HasData(
            new Receipt
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                ExpenseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                OwnerUserId = "u-emma",
                BlobUri = "seed-pending",
                ContentType = "text/plain",
                UploadedAt = new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero)
            },
            new Receipt
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000002"),
                ExpenseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                OwnerUserId = "u-mateo",
                BlobUri = "seed-pending",
                ContentType = "text/plain",
                UploadedAt = new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero)
            });
    }
}
