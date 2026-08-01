using Meridian.DataAccess;
using Meridian.Services.DTOs;
using Meridian.Services.Mapping;

namespace Meridian.Services;

public sealed class ReportingService(IReportingRepository repository) : IReportingService
{
    public async Task<IReadOnlyList<DepartmentSpendSummaryDto>> GetDepartmentSpendAsync(CallerContext caller, CancellationToken ct)
    {
        var summaries = caller.IsFinance
            ? await repository.GetAllAsync(ct)
            : await repository.GetByDepartmentAsync(caller.Department ?? string.Empty, ct);
        return summaries.Select(s => s.ToDto()).ToList();
    }
}
