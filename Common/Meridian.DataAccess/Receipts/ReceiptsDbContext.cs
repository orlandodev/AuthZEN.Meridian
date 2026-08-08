using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess.Receipts;

public class ReceiptsDbContext(DbContextOptions<ReceiptsDbContext> options) : DbContext(options)
{
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BlobUri is a placeholder here ("seed-pending") because the real
        // blob URI is only known at runtime (it embeds the Azurite/Storage
        // host:port, which doesn't exist at migration-authoring time). See
        // ReceiptBlobContentSeeder, run from Program.cs at startup, which
        // uploads the actual placeholder content and updates BlobUri by
        // these fixed ids. ExpenseId values are fixed illustrative GUIDs,
        // NOT foreign keys into Expenses.Api's database: Receipts.Api and
        // Expenses.Api are separate services/databases with no referential
        // integrity between them.
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
