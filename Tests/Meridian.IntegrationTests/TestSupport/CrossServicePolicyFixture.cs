using Meridian.DataAccess.Models;
using Meridian.DataAccess.PdP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Meridian.IntegrationTests.TestSupport;

// One Pdp.Service shared by in-process Expenses.Api, Receipts.Api and
// Reporting.Api hosts, so a single edit to the policy database can be observed
// through all three over real HTTP. Dedicated to CrossServicePolicyReloadTests
// (not shared with other test classes) because its test mutates policy data
// mid-run. The Postgres container is only for Expenses.Api's relational-only
// ExecuteUpdateAsync — the PDP stays on EF Core InMemory.
public sealed class CrossServicePolicyFixture : IAsyncLifetime
{
    private const string ManagerUserId = "u-nadia";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    public PdpApiFactory Pdp { get; } = new("integration-tests-crossservice-policydb");

    public ExpensesApiFactory Expenses { get; private set; } = null!;

    public ReceiptsApiFactory Receipts { get; private set; } = null!;

    public ReportingApiFactory Reporting { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Expenses = new ExpensesApiFactory(Pdp, _postgres.GetConnectionString());
        Receipts = new ReceiptsApiFactory(Pdp, "integration-tests-crossservice-receiptsdb");
        Reporting = new ReportingApiFactory(Pdp, "integration-tests-crossservice-reportingdb");

        // Accessing Services forces each host to build, running its own startup
        // pipeline (migrate-or-create and HasData seeding) exactly as in
        // production. No extra seeding: the existing seed rows cover every
        // assertion.
        _ = Expenses.Services;
        _ = Receipts.Services;
        _ = Reporting.Services;
    }

    public async Task DisposeAsync()
    {
        Expenses.Dispose();
        Receipts.Dispose();
        Reporting.Dispose();
        Pdp.Dispose();
        await _postgres.DisposeAsync();
    }

    // RoleAssignment is init-only, so "changing" u-nadia's role is a delete +
    // re-insert on the same key. RuleWorkspace re-queries AsNoTracking on every
    // evaluation, so the next request through any of the three APIs sees it.
    public async Task SetManagerRoleAsync(string role)
    {
        using var scope = Pdp.Services.CreateScope();
        var policyDb = scope.ServiceProvider.GetRequiredService<PolicyDbContext>();

        var current = await policyDb.RoleAssignments.SingleAsync(assignment => assignment.UserId == ManagerUserId);
        policyDb.RoleAssignments.Remove(current);
        await policyDb.SaveChangesAsync();

        policyDb.RoleAssignments.Add(new RoleAssignment
        {
            UserId = ManagerUserId,
            Role = role,
            Department = current.Department
        });
        await policyDb.SaveChangesAsync();
    }
}
