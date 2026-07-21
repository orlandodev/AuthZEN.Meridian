using System.ComponentModel.DataAnnotations;

namespace Meridian.ExpensePortal.Models;

// Mirrors Meridian.DataAccess.Models.Expense. Kept as a separate, portal-owned
// type rather than a shared project reference on purpose: the portal is a client
// of the API's public contract, not an internal consumer of its domain model.
// If this drifts in a real project, that's a signal to extract a small
// Meridian.Expenses.Contracts project.
public enum ExpenseStatus { Draft, Submitted, Approved, Rejected, Reimbursed }

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

// Department is derived server-side from the caller's own claim, not sent by the client.
// Validation attributes mirror Meridian.Services.DTOs.CreateExpenseRequest so bad input
// is caught by MVC model binding before the API is even called.
public sealed record CreateExpenseRequest(
    [property: Range(typeof(decimal), "0.01", "1000000")] decimal Amount,
    [property: Required, MaxLength(100)] string Category);

// Status is either Approved or Rejected; the API rejects anything else.
public sealed record UpdateExpenseStatusRequest(ExpenseStatus Status);
