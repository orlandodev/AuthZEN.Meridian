using System.Text.Json;
using AuthZen.Contracts;
using FluentAssertions;
using Meridian.AuthZen.ConformanceTests.TestSupport;
using Meridian.Pdp.Core.Pdp;
using Meridian.Pdp.Opa.Pdp;
using Meridian.UnitTests.PdpService;
using Meridian.UnitTests.TestSupport;
using Xunit;

namespace Meridian.AuthZen.ConformanceTests;

// Runs every ConformanceCases request through both IPolicyEngine
// implementations — the homegrown PolicyRulesEngine and OpaPolicyEngine
// backed by a real OPA server — over the same policydb facts and host clock.
// Both must return the same, expected decision: the AuthZEN contract makes
// the PDPs interchangeable only if their decisions are.
public sealed class PdpConformanceTests(OpaContainerFixture opa) : IClassFixture<OpaContainerFixture>
{
    private static readonly TimeZoneInfo BusinessZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static TheoryData<string> CaseNames => new(ConformanceCases.All.Keys);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task EvaluateAsync_SameSarcRequest_BothPdpsReturnExpectedDecision(string caseName)
    {
        var testCase = ConformanceCases.All[caseName];
        var request = AsReceivedOverTheWire(testCase.Request);
        var clock = new FakeTimeProvider(testCase.Now);
        using var db = PolicyDbContextTestFactory.Create();
        using var opaClient = new HttpClient { BaseAddress = opa.BaseAddress };
        var homegrown = new PolicyRulesEngine(db, clock, BusinessZone);
        var opaBacked = new OpaPolicyEngine(opaClient, db, BusinessZone, clock);

        var decisions = (
            Homegrown: await homegrown.EvaluateAsync(request),
            Opa: await opaBacked.EvaluateAsync(request));

        decisions.Should().Be((testCase.Expected, testCase.Expected));
    }

    // A PDP host deserializes properties/context into JsonElement values, not
    // the CLR strings/decimals RequestFactory builds — round-trip so both
    // engines see the shape a real PEP call produces.
    private static AccessEvaluationRequest AsReceivedOverTheWire(AccessEvaluationRequest request) =>
        JsonSerializer.Deserialize<AccessEvaluationRequest>(JsonSerializer.Serialize(request))!;
}
