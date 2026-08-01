using Meridian.DataAccess.Models;

namespace Meridian.DataAccess;

public interface IReportingRepository
{
    Task<List<DepartmentSpendSummary>> GetAllAsync(CancellationToken ct);

    Task<List<DepartmentSpendSummary>> GetByDepartmentAsync(string department, CancellationToken ct);
}
