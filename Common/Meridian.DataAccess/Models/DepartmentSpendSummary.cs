namespace Meridian.DataAccess.Models;

public class DepartmentSpendSummary
{
    public Guid Id { get; set; }
    public string Department { get; set; } = default!;
    public string Period { get; set; } = default!;   // year-month, e.g. "2026-07"
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
}
