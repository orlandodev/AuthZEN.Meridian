namespace Meridian.IntegrationTests.TestSupport;

// Shared across every test in a test class (xUnit IClassFixture semantics).
// No Postgres container needed (unlike ExpensesPdpFixture) — ReceiptsDbContext
// runs entirely on EF Core's InMemory provider — so this just needs to force
// the host to build so Program.cs's own startup pipeline (migrate-or-create,
// the blob-content seeder) runs, applying ReceiptsDbContext's HasData rows
// (two receipts, owned by u-emma and u-mateo) exactly as it would in
// production.
public sealed class ReceiptsPdpFixture : IAsyncLifetime
{
    // Distinct InMemory database name from ExpensesPdpFixture's own
    // PdpApiFactory — see PdpApiFactory's constructor comment for why
    // sharing the default name would be a cross-fixture race.
    public PdpApiFactory Pdp { get; } = new("integration-tests-receipts-policydb");
    public ReceiptsApiFactory Receipts { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Receipts = new ReceiptsApiFactory(Pdp);
        _ = Receipts.Services; // forces the host to build
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Receipts.Dispose();
        Pdp.Dispose();
        return Task.CompletedTask;
    }
}
