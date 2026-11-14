using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuthZen.Contracts;
using Meridian.Pdp.Core.Pdp;
using Meridian.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meridian.UnitTests.PdpService;

// Other tests emit on the same process-wide "Meridian.AuthZen" source/meter in
// parallel, so every assertion filters on a policy ID, subject ID, or action
// unique to the test.
public class InstrumentedPolicyEngineTests
{
    [Fact]
    public async Task EvaluateAsync_AnyDecision_ReturnsInnerEngineDecisionUnchanged()
    {
        var expected = new PolicyDecision(true, UniquePolicyId());
        var engine = CreateEngine(InnerReturning(expected));

        var result = await engine.EvaluateAsync(Request());

        result.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_TagsDecideSpanWithPolicyId()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(false, policyId)));
        using var spans = new AuthZenSpanRecorder("authz.decide", "authz.policy.id", policyId);

        await engine.EvaluateAsync(Request());

        spans.Single().GetTagItem("authz.decision").Should().Be("deny");
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_EmitsInternalSpanBeneathTheRequestSpan()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var spans = new AuthZenSpanRecorder("authz.decide", "authz.policy.id", policyId);

        await engine.EvaluateAsync(Request());

        spans.Single().Kind.Should().Be(ActivityKind.Internal);
    }

    [Fact]
    public async Task EvaluateAsync_Deny_LeavesSpanStatusUnset()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(false, policyId)));
        using var spans = new AuthZenSpanRecorder("authz.decide", "authz.policy.id", policyId);

        await engine.EvaluateAsync(Request());

        spans.Single().Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_CountsDecisionTaggedWithPolicyId()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new AuthZenMeasurementRecorder("authz.decisions", "policy.id", policyId);

        await engine.EvaluateAsync(Request());

        recorder.Measurements.Should().ContainSingle()
            .Which.Tags.Should().Contain(new Dictionary<string, object?>
            {
                ["decision"] = "permit",
                ["action"] = "read",
                ["resource.type"] = "expense",
                ["policy.id"] = policyId
            });
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_MetricTagsNeverCarrySubjectOrResourceIds()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new AuthZenMeasurementRecorder("authz.decisions", "policy.id", policyId);

        await engine.EvaluateAsync(Request());

        recorder.Measurements.Should().ContainSingle()
            .Which.Tags.Keys.Should().BeEquivalentTo("decision", "action", "resource.type", "policy.id");
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_RecordsDurationInMillisecondsTaggedWithPolicyId()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new AuthZenMeasurementRecorder("authz.decision.duration", "policy.id", policyId);

        await engine.EvaluateAsync(Request());

        recorder.Instrument!.Unit.Should().Be("ms");
        recorder.Measurements.Should().ContainSingle()
            .Which.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EvaluateAsync_DurationHistogram_UsesFineGrainedBucketsForLowLatency()
    {
        var policyId = UniquePolicyId();
        var engine = CreateEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new AuthZenMeasurementRecorder("authz.decision.duration", "policy.id", policyId);

        await engine.EvaluateAsync(Request());

        var boundaries = ((Histogram<double>)recorder.Instrument!).Advice!.HistogramBucketBoundaries;
        boundaries.Should().NotBeNull()
            .And.Subject.Count(b => b <= 50).Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task EvaluateAsync_InnerEngineThrows_RethrowsTheSameException()
    {
        var failure = new HttpRequestException("OPA unreachable");
        var engine = CreateEngine(InnerThrowing(failure));

        var act = () => engine.EvaluateAsync(Request());

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.Should().BeSameAs(failure);
    }

    [Theory]
    [InlineData("authz.decisions")]
    [InlineData("authz.decision.duration")]
    public async Task EvaluateAsync_InnerEngineThrows_RecordsErrorDecisionWithErrorType(string instrumentName)
    {
        var action = UniqueAction();
        var engine = CreateEngine(InnerThrowing(new HttpRequestException("OPA unreachable")));
        using var recorder = new AuthZenMeasurementRecorder(instrumentName, "action", action);

        var act = () => engine.EvaluateAsync(Request(action: action));

        await act.Should().ThrowAsync<HttpRequestException>();
        recorder.Measurements.Should().ContainSingle()
            .Which.Tags.Should().Contain(new Dictionary<string, object?>
            {
                ["decision"] = "error",
                ["error.type"] = "System.Net.Http.HttpRequestException",
                ["policy.id"] = null
            });
    }

    [Fact]
    public async Task EvaluateAsync_InnerEngineThrows_MarksSpanError()
    {
        var subjectId = UniqueSubjectId();
        var engine = CreateEngine(InnerThrowing(new InvalidOperationException("policydb down")));
        using var spans = new AuthZenSpanRecorder("authz.decide", "authz.subject.id", subjectId);

        var act = () => engine.EvaluateAsync(Request(subjectId));

        await act.Should().ThrowAsync<InvalidOperationException>();
        var span = spans.Single();
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("authz.decision").Should().Be("error");
    }

    [Fact]
    public async Task EvaluateAsync_CallerCancels_RecordsNoErrorDecision()
    {
        var action = UniqueAction();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var engine = CreateEngine(InnerThrowing(new OperationCanceledException(cts.Token)));
        using var recorder = new AuthZenMeasurementRecorder("authz.decisions", "action", action);

        var act = () => engine.EvaluateAsync(Request(action: action), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        recorder.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_WritesOneStructuredDecisionLogLine()
    {
        var policyId = UniquePolicyId();
        var subjectId = UniqueSubjectId();
        var logger = new CapturingLogger<InstrumentedPolicyEngine>();
        var engine = new InstrumentedPolicyEngine(InnerReturning(new PolicyDecision(true, policyId)), logger);

        await engine.EvaluateAsync(Request(subjectId));

        logger.Records.Should().ContainSingle()
            .Which.Should().Match<CapturedLogRecord>(record =>
                record.Level == LogLevel.Information
                && Equals(record.Fields["Decision"], "permit")
                && Equals(record.Fields["SubjectId"], subjectId)
                && Equals(record.Fields["Action"], "read")
                && Equals(record.Fields["ResourceType"], "expense")
                && Equals(record.Fields["ResourceId"], "expense-1")
                && Equals(record.Fields["PolicyId"], policyId));
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_WritesLogLineUnderTheDecideSpan()
    {
        var policyId = UniquePolicyId();
        var logger = new CapturingLogger<InstrumentedPolicyEngine>();
        var engine = new InstrumentedPolicyEngine(InnerReturning(new PolicyDecision(true, policyId)), logger);
        using var spans = new AuthZenSpanRecorder("authz.decide", "authz.policy.id", policyId);

        await engine.EvaluateAsync(Request());

        var span = spans.Single();
        logger.Records.Should().ContainSingle()
            .Which.Should().Match<CapturedLogRecord>(record =>
                record.TraceId == span.TraceId && record.SpanId == span.SpanId);
    }

    [Fact]
    public async Task EvaluateAsync_InnerEngineThrows_LogsWarningWithException()
    {
        var failure = new InvalidOperationException("policydb down");
        var logger = new CapturingLogger<InstrumentedPolicyEngine>();
        var engine = new InstrumentedPolicyEngine(InnerThrowing(failure), logger);

        var act = () => engine.EvaluateAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Records.Should().ContainSingle()
            .Which.Should().Match<CapturedLogRecord>(record =>
                record.Level == LogLevel.Warning && record.Exception == failure);
    }

    private static string UniquePolicyId() => $"test.{Guid.NewGuid():N}";

    private static string UniqueSubjectId() => $"u-test-{Guid.NewGuid():N}";

    private static string UniqueAction() => $"test-action-{Guid.NewGuid():N}";

    private static InstrumentedPolicyEngine CreateEngine(IPolicyEngine inner) =>
        new(inner, NullLogger<InstrumentedPolicyEngine>.Instance);

    private static IPolicyEngine InnerReturning(PolicyDecision decision)
    {
        var inner = new Mock<IPolicyEngine>();
        inner.Setup(e => e.EvaluateAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);
        return inner.Object;
    }

    private static IPolicyEngine InnerThrowing(Exception exception)
    {
        var inner = new Mock<IPolicyEngine>();
        inner.Setup(e => e.EvaluateAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return inner.Object;
    }

    private static AccessEvaluationRequest Request(string subjectId = "u-emma", string action = "read") =>
        RequestFactory.ExpenseRequest(subjectId, action, ownerId: subjectId, status: "Draft");
}
