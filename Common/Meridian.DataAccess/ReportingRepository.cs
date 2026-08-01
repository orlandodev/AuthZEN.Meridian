using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Meridian.DataAccess;

public sealed class ReportingRepository(ReportingDbContext db) : IReportingRepository
{
    public Task<List<DepartmentSpendSummary>> GetAllAsync(CancellationToken ct) =>
        db.DepartmentSpendSummaries.AsNoTracking().ToListAsync(ct);

    public Task<List<DepartmentSpendSummary>> GetByDepartmentAsync(string department, CancellationToken ct) =>
        db.DepartmentSpendSummaries.AsNoTracking().Where(s => s.Department == department).ToListAsync(ct);
}
