namespace Meridian.UnitTests.TestSupport;

// Minimal TimeProvider test double: fixed instant, no timer/timezone support needed here.
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
