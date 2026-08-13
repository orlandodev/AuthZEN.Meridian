using Meridian.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using static Meridian.DataAccess.PdP.PolicyConstants;

namespace Meridian.DataAccess.PdP;

public class PolicyDbContext(DbContextOptions<PolicyDbContext> options) : DbContext(options)
{
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<ManagerOf> ManagerOfs => Set<ManagerOf>();
    public DbSet<AmountLimitConfig> AmountLimits => Set<AmountLimitConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed values are fixed literals: HasData is baked into the compiled
        // migration at `migrations add` time, not executed at startup — a
        // non-deterministic value here would regenerate differently on every
        // OnModelCreating call, which EF sees as a spurious model change.
        // See TestUsers.cs's four users.
        modelBuilder.Entity<RoleAssignment>(e =>
        {
            e.HasKey(r => r.UserId);
            e.HasData(
                new RoleAssignment { UserId = "u-emma", Role = RoleNames.Employee, Department = "Sales" },
                new RoleAssignment { UserId = "u-mateo", Role = RoleNames.Employee, Department = "Sales" },
                new RoleAssignment { UserId = "u-nadia", Role = RoleNames.Manager, Department = "Sales" },
                new RoleAssignment { UserId = "u-finn", Role = RoleNames.Finance, Department = "Finance" });
        });

        modelBuilder.Entity<ManagerOf>(e =>
        {
            e.HasKey(m => new { m.ManagerUserId, m.ReportUserId });
            e.HasData(
                new ManagerOf { ManagerUserId = "u-nadia", ReportUserId = "u-emma" },
                new ManagerOf { ManagerUserId = "u-nadia", ReportUserId = "u-mateo" });
        });

        modelBuilder.Entity<AmountLimitConfig>(e =>
        {
            e.HasKey(a => a.Key);
            e.HasData(
                new AmountLimitConfig { Key = AmountLimitKeys.ExpenseApproveManagerLimit, Value = 5000m });
        });
    }
}
