using Meridian.DataAccess;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;
using Meridian.Services.Mapping;

namespace Meridian.Services;

public sealed class ExpenseService(IExpenseRepository repository) : IExpenseService
{
    public async Task<IReadOnlyList<ExpenseDto>> GetVisibleExpensesAsync(CallerContext caller, CancellationToken ct)
    {
        var expenses = caller.IsFinance
            ? await repository.GetAllAsync(ct)
            : await repository.GetByOwnerAsync(caller.UserId, ct);
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
