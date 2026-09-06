namespace Meridian.Pdp.Service.Reporting;

// The export window: Monday-Friday, 9am-5pm in the organization's business
// timezone. Evaluated against the PDP's own TimeProvider, not a caller-supplied
// value — a dishonest or buggy PEP could otherwise bypass this by lying about
// the time. The timezone comes from configuration (BusinessHours:TimeZone),
// resolved once at the composition root with no fallback; 9am-5pm is checked in
// that zone, so the window tracks DST automatically.
public static class BusinessHoursPolicy
{
    private static readonly TimeOnly OpensAt = new(9, 0);
    private static readonly TimeOnly ClosesAt = new(17, 0);

    public static bool IsWithinBusinessHours(TimeProvider timeProvider, TimeZoneInfo businessTimeZone)
    {
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), businessTimeZone);
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var timeOfDay = TimeOnly.FromTimeSpan(now.TimeOfDay);
        return timeOfDay >= OpensAt && timeOfDay < ClosesAt;
    }
}
