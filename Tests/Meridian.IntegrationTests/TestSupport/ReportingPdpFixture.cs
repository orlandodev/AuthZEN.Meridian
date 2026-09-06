namespace Meridian.IntegrationTests.TestSupport;

// Shared across every test in ReportingApiPdpIntegrationTests (xUnit
// IClassFixture semantics). No Postgres container needed (unlike
// ExpensesPdpFixture) — ReportingDbContext runs entirely on EF Core's InMemory
// provider — so this just forces each host to build so Program.cs's own
// startup pipeline (migrate-or-create) applies ReportingDbContext's HasData
// rows exactly as it would in production.
//
// Two PDP hosts at fixed instants: one inside the Monday-Friday 9am-5pm export
// window (evaluated in America/New_York, the default business zone), one
// outside it (a Saturday). DepartmentSpendRules.CanExport reads the PDP's own
// clock, so pinning it is the only way to prove the business-hours half of the
// rule over real HTTP rather than only in RulesEngineTests.
public sealed class ReportingPdpFixture : IAsyncLifetime
{
    // 15:00 UTC is 11:00 in America/New_York (EDT) — inside the window.
    private static readonly DateTimeOffset WithinBusinessHours = new(2026, 7, 23, 15, 0, 0, TimeSpan.Zero);  // Thursday
    private static readonly DateTimeOffset OutsideBusinessHours = new(2026, 7, 25, 15, 0, 0, TimeSpan.Zero); // Saturday

    public PdpApiFactory Pdp { get; } =
        new("integration-tests-reporting-policydb", new FakeTimeProvider(WithinBusinessHours));

    public PdpApiFactory PdpOutsideHours { get; } =
        new("integration-tests-reporting-policydb-offhours", new FakeTimeProvider(OutsideBusinessHours));

    public ReportingApiFactory Reporting { get; private set; } = null!;
    public ReportingApiFactory ReportingOutsideHours { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Reporting = new ReportingApiFactory(Pdp);
        ReportingOutsideHours = new ReportingApiFactory(PdpOutsideHours);
        _ = Reporting.Services;              // forces the host to build
        _ = ReportingOutsideHours.Services;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Reporting.Dispose();
        ReportingOutsideHours.Dispose();
        Pdp.Dispose();
        PdpOutsideHours.Dispose();
        return Task.CompletedTask;
    }
}
