namespace Meridian.Pdp.Service.Reporting;

// Mirrors Meridian.Reporting.Api's BusinessHoursPolicy (same Mon-Fri,
// 9am-5pm UTC window) — duplicated rather than referenced, since the PDP is
// meant to stay a standalone, swappable component with no ProjectReference
// back to any PEP. Evaluated against the PDP's own TimeProvider, not a
// caller-supplied context value: unlike the amount-limit check (which needs
// transaction-specific data only the caller has), "what time is it right
// now" is something the PDP can determine on its own — trusting a
// caller-supplied timestamp here would let a dishonest or buggy PEP bypass
// the restriction outright.
public static class BusinessHoursPolicy
{
    private static readonly TimeOnly OpensAt = new(9, 0);
    private static readonly TimeOnly ClosesAt = new(17, 0);

    public static bool IsWithinBusinessHours(TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var timeOfDay = TimeOnly.FromTimeSpan(now.TimeOfDay);
        return timeOfDay >= OpensAt && timeOfDay < ClosesAt;
    }
}
