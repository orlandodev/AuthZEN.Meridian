using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;
using Meridian.Services.Mapping;

namespace Meridian.Services;

public sealed class ExpenseService(IExpenseRepository repository) : IExpenseService
{
    // Mirrors OwnerOrPrivilegedHandler's rule (owner, finance, or same-department
    // manager) as a query scope instead of a per-resource check, since there's no
    // single ExpenseDto here to evaluate the handler against.
    public async Task<IReadOnlyList<ExpenseDto>> GetVisibleExpensesAsync(CallerContext caller, CancellationToken ct)
    {
        var expenses = caller switch
        {
            { IsFinance: true } => await repository.GetAllAsync(ct),
            { IsManager: true, Department: not null } => await repository.GetByDepartmentAsync(caller.Department, ct),
            _ => await repository.GetByOwnerAsync(caller.UserId, ct)
        };
        return expenses.Select(e => e.ToDto()).ToList();
    }

    public async Task<ExpenseDto?> GetByIdAsync(Guid id, CancellationToken ct) =>
        (await repository.GetByIdAsync(id, ct))?.ToDto();

    public async Task<ExpenseDto?> CreateAsync(CreateExpenseRequest request, CallerContext caller, CancellationToken ct)
    {
        if (caller.Department is null)
        {
            return null;
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OwnerUserId = caller.UserId,
            Department = caller.Department,
            Amount = request.Amount,
            Category = request.Category,
            Status = ExpenseStatus.Draft
        };

        await repository.AddAsync(expense, ct);
        await repository.SaveChangesAsync(ct);
        return expense.ToDto();
    }

    public async Task<ExpenseDto?> DecideAsync(Guid id, ExpenseStatus decision, string deciderUserId, CancellationToken ct)
    {
        var decidedAt = DateTimeOffset.UtcNow;
        var rowsUpdated = await repository.TryDecideAsync(id, decision, deciderUserId, decidedAt, ct);
        if (rowsUpdated == 0)
        {
            return null;
        }

        var expense = await repository.GetByIdAsync(id, ct);
        return expense?.ToDto();
    }
}
