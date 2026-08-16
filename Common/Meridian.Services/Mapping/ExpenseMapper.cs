using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.Services.Mapping;

public static class ExpenseMapper
{
    public static ExpenseDto ToDto(this Expense expense) => new(
        expense.Id,
        expense.OwnerUserId,
        expense.Department,
        expense.Amount,
        expense.Currency,
        expense.Category,
        expense.Status,
        expense.ApproverUserId,
        expense.CreatedAt,
        expense.DecidedAt,
        expense.RejectionReason);
}
