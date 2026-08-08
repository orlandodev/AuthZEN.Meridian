namespace Meridian.DataAccess.Models;

// Explicit manager -> report relationship. Composite natural key — no
// surrogate id needed, which sidesteps non-deterministic seed-id concerns
// for this table entirely.
public sealed class ManagerOf
{
    public required string ManagerUserId { get; init; }
    public required string ReportUserId { get; init; }
}
