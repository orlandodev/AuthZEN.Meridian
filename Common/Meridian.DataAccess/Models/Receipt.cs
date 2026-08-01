namespace Meridian.DataAccess.Models;

public class Receipt
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public string OwnerUserId { get; set; } = default!;   // maps to the JWT 'sub'
    public string BlobUri { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    // Deliberately no Department field — see Receipts.Api/Authorization/OwnerOrPrivilegedHandler.cs
    // for why that's the Stage 1 drift bug, not an oversight.
}
