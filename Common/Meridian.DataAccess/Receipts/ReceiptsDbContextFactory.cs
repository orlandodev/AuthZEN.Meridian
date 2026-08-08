using Microsoft.EntityFrameworkCore.Design;

namespace Meridian.DataAccess.Receipts;

public sealed class ReceiptsDbContextFactory : IDesignTimeDbContextFactory<ReceiptsDbContext>
{
    public ReceiptsDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Build<ReceiptsDbContext>("receiptsdb"));
}
