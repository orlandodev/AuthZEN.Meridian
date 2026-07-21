namespace Meridian.DataAccess.Models;

public class Expense
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = default!;   // maps to the JWT 'sub'
    public string Department { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Category { get; set; } = default!;
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
    public string? ApproverUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
}
