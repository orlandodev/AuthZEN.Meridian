namespace Meridian.DataAccess.Models;

// Generic named numeric threshold store (string key -> decimal), rather than
// a single-purpose "ManagerLimit" column, so future limits (e.g. a finance
// per-transaction cap) don't require a schema change.
public sealed class AmountLimitConfig
{
    public required string Key { get; init; }
    public required decimal Value { get; init; }
}
