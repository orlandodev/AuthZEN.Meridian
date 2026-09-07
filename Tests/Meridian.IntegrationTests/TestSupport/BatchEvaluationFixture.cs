using AuthZen.Pep;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.IntegrationTests.TestSupport;

// Hands a test one correctly-wired IPolicyDecisionClient pointed at an
// in-process Pdp.Service, plus a fixed set of synthetic candidate expenses to
// authorize. No Postgres container and no Expenses.Api host: the batch call
// under test (AuthZenPolicyDecisionClient -> POST /access/v1/evaluations ->
// PolicyRulesEngine) touches no expense table, only the policy database. The
// client is resolved from a ReportingApiFactory purely because it is the
// cheapest host that already wires IPolicyDecisionClient to the PDP handler the
// same way every PEP does — the registration is identical across the three
// services.
public sealed class BatchEvaluationFixture : IAsyncLifetime
{
    private const int CandidateCount = 50;

    private IServiceScope _scope = null!;

    public PdpApiFactory Pdp { get; } = new("integration-tests-batcheval-policydb");

    public ReportingApiFactory PdpClientHost { get; private set; } = null!;

    public IPolicyDecisionClient PolicyDecisionClient { get; private set; } = null!;

    public IReadOnlyList<CandidateExpense> Candidates { get; } = BuildCandidates();

    public Task InitializeAsync()
    {
        PdpClientHost = new ReportingApiFactory(Pdp, "integration-tests-batcheval-reportingdb");
        _scope = PdpClientHost.Services.CreateScope();
        PolicyDecisionClient = _scope.ServiceProvider.GetRequiredService<IPolicyDecisionClient>();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        PdpClientHost.Dispose();
        Pdp.Dispose();
        return Task.CompletedTask;
    }

    // A deterministic allow/deny mix for subject u-nadia (a Sales manager in the
    // PDP seed who manages u-emma and u-mateo): u-emma's/u-mateo's Submitted
    // expenses are readable; Draft expenses hit the manager draft carve-out;
    // u-ghost has no policy row at all. Every fifth entry is u-ghost; odd
    // entries are Draft.
    private static IReadOnlyList<CandidateExpense> BuildCandidates()
    {
        var candidates = new List<CandidateExpense>(CandidateCount);
        for (var index = 0; index < CandidateCount; index++)
        {
            candidates.Add(new CandidateExpense(
                Guid.NewGuid(),
                OwnerForIndex(index),
                index % 2 == 0 ? "Submitted" : "Draft"));
        }

        return candidates;
    }

    private static string OwnerForIndex(int index)
    {
        if (index % 5 == 0)
        {
            return "u-ghost";
        }

        return index % 2 == 0 ? "u-emma" : "u-mateo";
    }
}
