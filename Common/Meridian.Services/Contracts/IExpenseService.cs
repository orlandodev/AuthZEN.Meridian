using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.Services.Contracts;

public interface IExpenseService
{
    // Finance sees every expense; managers see their department's expenses;
    // everyone else sees only their own.
    Task<IReadOnlyList<ExpenseDto>> GetVisibleExpensesAsync(CallerContext caller, CancellationToken ct);

    Task<ExpenseDto?> GetByIdAsync(Guid id, CancellationToken ct);

    // Returns null if the caller has no department claim to derive the expense's
    // department from.
    Task<ExpenseDto?> CreateAsync(CreateExpenseRequest request, CallerContext caller, CancellationToken ct);

    // decision is Approved or Rejected. Returns null if the expense no longer
    // exists or is no longer Submitted by the time this runs (a race against
    // the caller's own pre-check read, or a concurrent decision).
    Task<ExpenseDto?> DecideAsync(Guid id, ExpenseStatus decision, string deciderUserId, CancellationToken ct);
}
