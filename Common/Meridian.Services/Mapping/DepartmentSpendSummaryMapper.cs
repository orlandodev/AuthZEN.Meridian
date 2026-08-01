using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.Services.Mapping;

public static class DepartmentSpendSummaryMapper
{
    public static DepartmentSpendSummaryDto ToDto(this DepartmentSpendSummary summary) => new(
        summary.Id,
        summary.Department,
        summary.Period,
        summary.TotalAmount,
        summary.Currency);
}
