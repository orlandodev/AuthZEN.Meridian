using Meridian.DataAccess.Models;

namespace Meridian.Services.DTOs;

public sealed record ExpenseDto(
    Guid Id,
    string OwnerUserId,
    string Department,
    decimal Amount,
    string Currency,
    string Category,
    ExpenseStatus Status,
    string? ApproverUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);
