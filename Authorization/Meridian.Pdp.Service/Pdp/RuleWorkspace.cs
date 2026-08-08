using Meridian.DataAccess.Models;
using Meridian.DataAccess.PdP;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Pdp.Service.Pdp;

// Per-evaluate-call DB access (with a small subject-profile cache so a
// boxcarred /access/v1/evaluations request doesn't re-query the same
// subject's RoleAssignment repeatedly within one HTTP call) plus the other
// per-request primitives rules need — currently just the clock. Kept
// domain-agnostic: policy decisions (e.g. what counts as "business hours")
// live in the *Rules classes, not here.
public sealed class RuleWorkspace(PolicyDbContext db, TimeProvider timeProvider)
{
    private readonly Dictionary<string, RoleAssignment?> _profileCache = new();

    public TimeProvider TimeProvider { get; } = timeProvider;

    public async Task<RoleAssignment?> GetProfileAsync(string userId, CancellationToken ct)
    {
        if (_profileCache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var profile = await db.RoleAssignments.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId, ct);
        _profileCache[userId] = profile;
        return profile;
    }

    public Task<bool> IsManagerOfAsync(string managerId, string reportUserId, CancellationToken ct) =>
        db.ManagerOfs.AsNoTracking()
            .AnyAsync(m => m.ManagerUserId == managerId && m.ReportUserId == reportUserId, ct);

    public async Task<decimal> GetAmountLimitAsync(string key, CancellationToken ct)
    {
        var cfg = await db.AmountLimits.AsNoTracking().SingleOrDefaultAsync(a => a.Key == key, ct);
        // Fail-closed: a missing config row means "never approve," not "unlimited."
        return cfg?.Value ?? 0m;
    }
}
