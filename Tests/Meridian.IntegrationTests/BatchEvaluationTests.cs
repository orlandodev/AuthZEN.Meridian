using System.Collections.Concurrent;
using System.Diagnostics;
using AuthZen.Contracts;
using Meridian.IntegrationTests.TestSupport;
using Xunit.Abstractions;

namespace Meridian.IntegrationTests;

// Proves and measures the batched authorization call already shipping in
// Meridian: ExpenseVisibilityFilter narrows a manager's department-scoped
// candidate expenses with a single POST /access/v1/evaluations instead of one
// POST /access/v1/evaluation per candidate. Both tests drive the real
// IPolicyDecisionClient against an in-process Pdp.Service.
public class BatchEvaluationTests(BatchEvaluationFixture fixture, ITestOutputHelper output)
    : IClassFixture<BatchEvaluationFixture>
{
    private const string ManagerSubjectId = "u-nadia";

    // Wraps each measured phase so its client spans can be told apart from the
    // authz.evaluate spans other test classes emit on the same "Meridian.AuthZen"
    // ActivitySource while the suite runs in parallel — the phase activity's
    // TraceId flows to every span started under it.
    private static readonly ActivitySource MeasurementSource = new("Meridian.IntegrationTests.BatchEvaluation");

    [Fact]
    public async Task AreAllowedAsync_OverCandidateExpenses_MatchesSequentialSingleCalls()
    {
        var batch = BuildBatch();

        var batchDecisions = await fixture.PolicyDecisionClient.AreAllowedAsync(batch);

        var sequentialDecisions = new List<bool>(batch.Evaluations.Count);
        foreach (var entry in batch.Evaluations)
        {
            sequentialDecisions.Add(await fixture.PolicyDecisionClient.IsAllowedAsync(ToSingleRequest(entry, batch)));
        }

        batchDecisions.Should().HaveCount(batch.Evaluations.Count);
        batchDecisions.Should().Equal(sequentialDecisions);
        batchDecisions.Should().Contain(true).And.Contain(false);
    }

    [Fact]
    public async Task AreAllowedAsync_VersusNSequentialSingleCalls_SpendsLessTimeInThePdp()
    {
        var batch = BuildBatch();
        var candidateCount = batch.Evaluations.Count;

        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name is "Meridian.AuthZen" or "Meridian.IntegrationTests.BatchEvaluation",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);

        const int warmupRuns = 3;
        const int measuredRuns = 10;

        var batchMillis = new List<double>(measuredRuns);
        var sequentialMillis = new List<double>(measuredRuns);

        for (var run = 0; run < warmupRuns + measuredRuns; run++)
        {
            spans.Clear();
            var batchTrace = await MeasurePhaseAsync(() => fixture.PolicyDecisionClient.AreAllowedAsync(batch));
            var batchDuration = spans
                .Single(span => span.TraceId == batchTrace && span.OperationName == "authz.evaluate.batch")
                .Duration;

            spans.Clear();
            var sequentialTrace = await MeasurePhaseAsync(async () =>
            {
                foreach (var entry in batch.Evaluations)
                {
                    await fixture.PolicyDecisionClient.IsAllowedAsync(ToSingleRequest(entry, batch));
                }
            });
            var singleDurations = spans
                .Where(span => span.TraceId == sequentialTrace && span.OperationName == "authz.evaluate")
                .Select(span => span.Duration)
                .ToList();
            singleDurations.Should().HaveCount(candidateCount);

            if (run < warmupRuns)
            {
                continue;
            }

            batchMillis.Add(batchDuration.TotalMilliseconds);
            sequentialMillis.Add(singleDurations.Sum(duration => duration.TotalMilliseconds));
        }

        var batchMean = batchMillis.Average();
        var sequentialMean = sequentialMillis.Average();

        output.WriteLine($"candidates per run         : {candidateCount}");
        output.WriteLine($"measured runs             : {measuredRuns} (after {warmupRuns} warm-up)");
        output.WriteLine($"batch      mean / p95 (ms): {batchMean:F2} / {Percentile(batchMillis, 95):F2}");
        output.WriteLine($"sequential mean / p95 (ms): {sequentialMean:F2} / {Percentile(sequentialMillis, 95):F2}");
        output.WriteLine($"sequential / batch        : {sequentialMean / batchMean:F1}x");
        output.WriteLine(
            $"per-item (us) batch / seq : {batchMean / candidateCount * 1000:F1} / {sequentialMean / candidateCount * 1000:F1}");

        batchMean.Should().BeLessThanOrEqualTo(sequentialMean);
    }

    private static async Task<ActivityTraceId> MeasurePhaseAsync(Func<Task> phase)
    {
        using var activity = MeasurementSource.StartActivity("batch-evaluation-phase");
        await phase();
        return activity!.TraceId;
    }

    private AccessEvaluationsRequest BuildBatch() =>
        new()
        {
            Subject = new Subject { Type = "user", Id = ManagerSubjectId },
            Action = new AuthZenAction { Name = "read" },
            Evaluations = fixture.Candidates
                .Select(candidate => new EvaluationEntry
                {
                    Resource = new Resource
                    {
                        Type = "expense",
                        Id = candidate.Id.ToString(),
                        Properties = new Dictionary<string, object>
                        {
                            ["ownerId"] = candidate.OwnerId,
                            ["status"] = candidate.Status
                        }
                    }
                })
                .ToList()
        };

    // Mirrors the PDP's own TryMergeWithDefaults: an entry inherits the batch's
    // subject/action/resource/context wherever it doesn't set its own.
    private static AccessEvaluationRequest ToSingleRequest(EvaluationEntry entry, AccessEvaluationsRequest batch) =>
        new()
        {
            Subject = entry.Subject ?? batch.Subject!,
            Action = entry.Action ?? batch.Action!,
            Resource = entry.Resource ?? batch.Resource!,
            Context = entry.Context ?? batch.Context
        };

    private static double Percentile(IReadOnlyList<double> values, int percentile)
    {
        var ordered = values.OrderBy(value => value).ToList();
        var rank = (int)Math.Ceiling(percentile / 100.0 * ordered.Count) - 1;
        return ordered[Math.Clamp(rank, 0, ordered.Count - 1)];
    }
}
