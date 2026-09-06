namespace Meridian.Reporting.Api.Authorization;

// Traditional in-process business rule gating the export endpoint: exports are
// only allowed Monday-Friday, 9am-5pm UTC. TimeProvider is injected rather than
// calling DateTime.Now/UtcNow directly so this is testable without wall-clock
// dependence.
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
