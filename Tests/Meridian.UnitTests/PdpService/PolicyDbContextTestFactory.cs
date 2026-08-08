using Meridian.DataAccess.PdP;
using Microsoft.EntityFrameworkCore;

namespace Meridian.UnitTests.PdpService;

// EF InMemory + EnsureCreated applies OnModelCreating's HasData seed for
// free, so tests exercise the exact same seed rows (the four test users,
// Nadia's ManagerOf rows, the 5000m manager limit) that the real migration
// bakes in.
public static class PolicyDbContextTestFactory
{
    public static PolicyDbContext Create()
    {
        var options = new DbContextOptionsBuilder<PolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PolicyDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
