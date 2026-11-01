using System.Diagnostics;
using System.Net;
using System.Text;
using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.UnitTests.ExpensePortal.TestSupport;
using Meridian.UnitTests.PdpService;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meridian.UnitTests.AuthZenPep;

public class AuthZenPolicyDecisionClientTests
{
    [Fact]
    public async Task IsAllowedAsync_PdpReportsPolicyId_TagsEvaluateSpanWithIt()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient("""{"decision":true,"context":{"policy":{"id":"expense.read"}}}""");
        using var spans = new EvaluateSpanRecorder(subjectId);

        await client.IsAllowedAsync(Request(subjectId));

        spans.Single().GetTagItem("authz.policy.id").Should().Be("expense.read");
    }

    [Fact]
    public async Task IsAllowedAsync_PdpReportsNoContext_LeavesPolicyIdTagUnset()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient("""{"decision":false}""");
        using var spans = new EvaluateSpanRecorder(subjectId);

        await client.IsAllowedAsync(Request(subjectId));

        spans.Single().GetTagItem("authz.policy.id").Should().BeNull();
    }

    [Fact]
    public async Task IsAllowedAsync_PdpReportsPolicyId_ReturnsOnlyTheDecision()
    {
        var client = CreateClient("""{"decision":true,"context":{"policy":{"id":"expense.read"}}}""");

        var allowed = await client.IsAllowedAsync(Request(UniqueSubjectId()));

        allowed.Should().BeTrue();
    }

    // Other tests emit "authz.evaluate" spans in parallel, so filter on a
    // subject unique to the test.
    private static string UniqueSubjectId() => $"u-test-{Guid.NewGuid():N}";

    private static AuthZenPolicyDecisionClient CreateClient(string pdpBody)
    {
        var http = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pdpBody, Encoding.UTF8, "application/json")
            },
            out _);
        return new AuthZenPolicyDecisionClient(http, NullLogger<AuthZenPolicyDecisionClient>.Instance);
    }

    private static AccessEvaluationRequest Request(string subjectId) =>
        RequestFactory.ExpenseRequest(subjectId, "read", ownerId: subjectId, status: "Draft");

    private sealed class EvaluateSpanRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _spans = [];

        public EvaluateSpanRecorder(string subjectId)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Meridian.AuthZen",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == "authz.evaluate"
                        && Equals(activity.GetTagItem("authz.subject.id"), subjectId))
                    {
                        lock (_spans)
                        {
                            _spans.Add(activity);
                        }
                    }
                }
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public Activity Single()
        {
            lock (_spans)
            {
                _spans.Should().ContainSingle();
                return _spans[0];
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
