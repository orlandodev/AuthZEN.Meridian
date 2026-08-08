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
        // Seed values are fixed literals, not computed at runtime: HasData is
        // baked into the compiled migration (and the model snapshot used to
        // diff future migrations) at `migrations add` time, not executed at
        // app startup. A non-deterministic value here would be captured once
        // into the migration and then regenerate differently on every
        // subsequent OnModelCreating call, which EF would see as a spurious
        // model change. See the four test users in TestUsers.cs.
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
