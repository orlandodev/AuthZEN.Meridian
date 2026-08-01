namespace Meridian.ExpensePortal.Models;

public sealed record DepartmentSpendSummary(
    string Department,
    string Period,
    decimal TotalAmount,
    string Currency);
