namespace Meridian.Services.DTOs;

public sealed record DepartmentSpendSummaryDto(
    Guid Id,
    string Department,
    string Period,
    decimal TotalAmount,
    string Currency);
