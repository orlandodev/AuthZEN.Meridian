using Microsoft.EntityFrameworkCore.Design;

namespace Meridian.DataAccess.PdP;

public sealed class PolicyDbContextFactory : IDesignTimeDbContextFactory<PolicyDbContext>
{
    public PolicyDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Build<PolicyDbContext>("policydb"));
}
