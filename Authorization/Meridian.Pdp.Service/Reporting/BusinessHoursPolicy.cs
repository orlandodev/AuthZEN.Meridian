namespace Meridian.Pdp.Service.Reporting;

// Mirrors Meridian.Reporting.Api's BusinessHoursPolicy (same Mon-Fri,
// 9am-5pm UTC window) — duplicated, not referenced, since the PDP stays a
// standalone component with no dependency back to any PEP. Evaluated
// against the PDP's own TimeProvider, not a caller-supplied value: a
// dishonest or buggy PEP could otherwise bypass this by lying about the time.
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
