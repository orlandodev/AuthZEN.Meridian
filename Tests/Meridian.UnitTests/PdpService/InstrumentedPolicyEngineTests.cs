using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuthZen.Contracts;
using Meridian.Pdp.Core.Pdp;
using Moq;

namespace Meridian.UnitTests.PdpService;

// Other tests emit on the same process-wide "Meridian.AuthZen" source/meter in
// parallel, so every assertion filters on a policy ID unique to the test.
public class InstrumentedPolicyEngineTests
{
    [Fact]
    public async Task EvaluateAsync_AnyDecision_ReturnsInnerEngineDecisionUnchanged()
    {
        var expected = new PolicyDecision(true, UniquePolicyId());
        var engine = new InstrumentedPolicyEngine(InnerReturning(expected));

        var result = await engine.EvaluateAsync(Request());

        result.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_TagsDecideSpanWithPolicyId()
    {
        var policyId = UniquePolicyId();
        var engine = new InstrumentedPolicyEngine(InnerReturning(new PolicyDecision(false, policyId)));
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Meridian.AuthZen",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("authz.policy.id"), policyId))
                {
                    lock (spans)
                    {
                        spans.Add(activity);
                    }
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        await engine.EvaluateAsync(Request());

        spans.Should().ContainSingle()
            .Which.Should().Match<Activity>(a =>
                a.OperationName == "authz.decide" && (string?)a.GetTagItem("authz.decision") == "deny");
    }

    [Fact]
    public async Task EvaluateAsync_AnyDecision_CountsDecisionTaggedWithPolicyId()
    {
        var policyId = UniquePolicyId();
        var engine = new InstrumentedPolicyEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new MeasurementRecorder("authz.decisions", policyId);

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
    public async Task EvaluateAsync_AnyDecision_RecordsDurationInMillisecondsTaggedWithPolicyId()
    {
        var policyId = UniquePolicyId();
        var engine = new InstrumentedPolicyEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new MeasurementRecorder("authz.decision.duration", policyId);

        await engine.EvaluateAsync(Request());

        recorder.Instrument!.Unit.Should().Be("ms");
        recorder.Measurements.Should().ContainSingle()
            .Which.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EvaluateAsync_DurationHistogram_UsesFineGrainedBucketsForLowLatency()
    {
        var policyId = UniquePolicyId();
        var engine = new InstrumentedPolicyEngine(InnerReturning(new PolicyDecision(true, policyId)));
        using var recorder = new MeasurementRecorder("authz.decision.duration", policyId);

        await engine.EvaluateAsync(Request());

        var boundaries = ((Histogram<double>)recorder.Instrument!).Advice!.HistogramBucketBoundaries;
        boundaries.Should().NotBeNull()
            .And.Subject.Count(b => b <= 50).Should().BeGreaterThanOrEqualTo(8);
    }

    private static string UniquePolicyId() => $"test.{Guid.NewGuid():N}";

    private static IPolicyEngine InnerReturning(PolicyDecision decision)
    {
        var inner = new Mock<IPolicyEngine>();
        inner.Setup(e => e.EvaluateAsync(It.IsAny<AccessEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);
        return inner.Object;
    }

    private static AccessEvaluationRequest Request() =>
        RequestFactory.ExpenseRequest("u-emma", "read", ownerId: "u-emma", status: "Draft");

    private sealed record Measurement(double Value, Dictionary<string, object?> Tags);

    // Captures measurements from one named "Meridian.AuthZen" instrument whose
    // policy.id tag matches.
    private sealed class MeasurementRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<Measurement> _measurements = [];

        public MeasurementRecorder(string instrumentName, string policyId)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Meridian.AuthZen" && instrument.Name == instrumentName)
                {
                    Instrument = instrument;
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) => Record(value, tags, policyId));
            _listener.SetMeasurementEventCallback<double>((_, value, tags, _) => Record(value, tags, policyId));
            _listener.Start();
        }

        public Instrument? Instrument { get; private set; }

        public IReadOnlyList<Measurement> Measurements
        {
            get
            {
                lock (_measurements)
                {
                    return [.. _measurements];
                }
            }
        }

        public void Dispose() => _listener.Dispose();

        private void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, string policyId)
        {
            var tagMap = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagMap[tag.Key] = tag.Value;
            }

            if (!Equals(tagMap.GetValueOrDefault("policy.id"), policyId))
            {
                return;
            }

            lock (_measurements)
            {
                _measurements.Add(new Measurement(value, tagMap));
            }
        }
    }
}
