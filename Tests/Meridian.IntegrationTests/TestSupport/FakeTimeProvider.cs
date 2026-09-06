namespace Meridian.IntegrationTests.TestSupport;

// Minimal TimeProvider test double: fixed instant, no timer/timezone support
// needed here. Mirrors Meridian.UnitTests/TestSupport/FakeTimeProvider.cs.
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
