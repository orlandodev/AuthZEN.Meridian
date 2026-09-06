using Meridian.Services.DTOs;

namespace Meridian.Services.Contracts;

public interface IReportingService
{
    // Finance sees every department's summary; a manager sees only their own
    // department's. Caller identity drives which repository query runs — the
    // same shape of visibility split as IExpenseService, reimplemented
    // independently here rather than shared.
    Task<IReadOnlyList<DepartmentSpendSummaryDto>> GetDepartmentSpendAsync(CallerContext caller, CancellationToken ct);
}
