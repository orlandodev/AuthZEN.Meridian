using Meridian.Reporting.Api.Authorization;
using Meridian.UnitTests.TestSupport;

namespace Meridian.UnitTests.ReportingApi.Authorization;

public class BusinessHoursPolicyTests
{
    // 2026-07-23 is a Thursday.
    private static TimeProvider At(int hour, int minute = 0) =>
        new FakeTimeProvider(new DateTimeOffset(2026, 7, 23, hour, minute, 0, TimeSpan.Zero));

    // 2026-07-25 is a Saturday.
    private static TimeProvider OnWeekendAt(int hour) =>
        new FakeTimeProvider(new DateTimeOffset(2026, 7, 25, hour, 0, 0, TimeSpan.Zero));

    [Fact]
    public void IsWithinBusinessHours_ReturnsTrue_AtOpeningTimeOnAWeekday()
    {
        BusinessHoursPolicy.IsWithinBusinessHours(At(9)).Should().BeTrue();
    }

    [Fact]
    public void IsWithinBusinessHours_ReturnsTrue_JustBeforeClosingOnAWeekday()
    {
        BusinessHoursPolicy.IsWithinBusinessHours(At(16, 59)).Should().BeTrue();
    }

    [Fact]
    public void IsWithinBusinessHours_ReturnsFalse_AtClosingTimeOnAWeekday()
    {
        BusinessHoursPolicy.IsWithinBusinessHours(At(17)).Should().BeFalse();
    }

    [Fact]
    public void IsWithinBusinessHours_ReturnsFalse_BeforeOpeningOnAWeekday()
    {
        BusinessHoursPolicy.IsWithinBusinessHours(At(8, 59)).Should().BeFalse();
    }

    [Fact]
    public void IsWithinBusinessHours_ReturnsFalse_DuringBusinessHoursOnAWeekend()
    {
        BusinessHoursPolicy.IsWithinBusinessHours(OnWeekendAt(12)).Should().BeFalse();
    }
}
