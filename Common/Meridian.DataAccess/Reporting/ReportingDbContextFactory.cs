using Microsoft.EntityFrameworkCore.Design;

namespace Meridian.DataAccess.Reporting;

public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Build<ReportingDbContext>("reportingdb"));
}
