using System.Diagnostics;
using System.Net;
using System.Text;
using AuthZen.Contracts;
using AuthZen.Pep;
using Meridian.UnitTests.ExpensePortal.TestSupport;
using Meridian.UnitTests.PdpService;
using Meridian.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meridian.UnitTests.AuthZenPep;

// Other tests emit on the same process-wide "Meridian.AuthZen" source/meter
// in parallel, so spans are filtered on a test-unique subject ID and
// authz.pep.errors measurements on a test-unique action name (subject IDs are
// deliberately never metric tags).
public class AuthZenPolicyDecisionClientTests
{
    [Fact]
    public async Task IsAllowedAsync_PdpReportsPolicyId_TagsEvaluateSpanWithIt()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient("""{"decision":true,"context":{"policy":{"id":"expense.read"}}}""");
        using var spans = new AuthZenSpanRecorder("authz.evaluate", "authz.subject.id", subjectId);

        await client.IsAllowedAsync(Request(subjectId));

        spans.Single().GetTagItem("authz.policy.id").Should().Be("expense.read");
    }

    [Fact]
    public async Task IsAllowedAsync_PdpReportsNoContext_LeavesPolicyIdTagUnset()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient("""{"decision":false}""");
        using var spans = new AuthZenSpanRecorder("authz.evaluate", "authz.subject.id", subjectId);

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

    [Fact]
    public async Task IsAllowedAsync_PdpDenies_LeavesSpanStatusUnset()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient("""{"decision":false}""");
        using var spans = new AuthZenSpanRecorder("authz.evaluate", "authz.subject.id", subjectId);

        await client.IsAllowedAsync(Request(subjectId));

        spans.Single().Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    [InlineData(HttpStatusCode.Unauthorized, "401")]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    public async Task IsAllowedAsync_PdpReturnsFailureStatus_CountsErrorTaggedWithStatusCode(
        HttpStatusCode status, string expectedErrorType)
    {
        var action = UniqueAction();
        var client = CreateClient(_ => new HttpResponseMessage(status));
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.IsAllowedAsync(Request(UniqueSubjectId(), action));

        await act.Should().ThrowAsync<HttpRequestException>();
        errors.Measurements.Should().ContainSingle()
            .Which.Tags.Should().Contain("error.type", expectedErrorType);
    }

    [Fact]
    public async Task IsAllowedAsync_PdpUnreachable_CountsPdpUnreachable()
    {
        var action = UniqueAction();
        var client = CreateClient(_ => throw new HttpRequestException("Connection refused"));
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.IsAllowedAsync(Request(UniqueSubjectId(), action));

        await act.Should().ThrowAsync<HttpRequestException>();
        errors.Measurements.Should().ContainSingle()
            .Which.Tags.Should().Contain("error.type", "pdp_unreachable");
    }

    [Fact]
    public async Task IsAllowedAsync_HttpClientTimesOut_CountsTimeout()
    {
        var action = UniqueAction();
        var client = CreateClient(_ => throw new TaskCanceledException("The request timed out."));
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.IsAllowedAsync(Request(UniqueSubjectId(), action));

        await act.Should().ThrowAsync<TaskCanceledException>();
        errors.Measurements.Should().ContainSingle()
            .Which.Tags.Should().Contain("error.type", "timeout");
    }

    [Fact]
    public async Task IsAllowedAsync_CallerCancels_DoesNotCountAnError()
    {
        var action = UniqueAction();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var client = CreateClient(_ => throw new TaskCanceledException());
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.IsAllowedAsync(Request(UniqueSubjectId(), action), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        errors.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task IsAllowedAsync_PdpFails_MarksSpanErrorWithErrorType()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var spans = new AuthZenSpanRecorder("authz.evaluate", "authz.subject.id", subjectId);

        var act = () => client.IsAllowedAsync(Request(subjectId));

        await act.Should().ThrowAsync<HttpRequestException>();
        var span = spans.Single();
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().Be("503");
    }

    [Fact]
    public async Task IsAllowedAsync_PdpFails_ErrorCounterCarriesOnlyLowCardinalityTags()
    {
        var action = UniqueAction();
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.IsAllowedAsync(Request(UniqueSubjectId(), action));

        await act.Should().ThrowAsync<HttpRequestException>();
        errors.Measurements.Should().ContainSingle()
            .Which.Tags.Keys.Should().BeEquivalentTo("error.type", "operation", "action", "resource.type");
    }

    [Fact]
    public async Task AreAllowedAsync_PdpDecidesEachItem_AddsOneDecisionEventPerItem()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient(
            """{"evaluations":[{"decision":true,"context":{"policy":{"id":"expense.read"}}},{"decision":false}]}""");
        using var spans = new AuthZenSpanRecorder("authz.evaluate.batch",
            span => span.Events.Any(e => Equals(e.Tags.FirstOrDefault(t => t.Key == "authz.subject.id").Value, subjectId)));

        await client.AreAllowedAsync(Batch(subjectId, "read", "expense-1", "expense-2"));

        spans.Single().Events
            .Select(e => (
                ResourceId: e.Tags.Single(t => t.Key == "authz.resource.id").Value,
                Decision: e.Tags.Single(t => t.Key == "authz.decision").Value))
            .Should().Equal(("expense-1", "permit"), ("expense-2", "deny"));
    }

    [Fact]
    public async Task AreAllowedAsync_PdpDecidesEachItem_TagsPermitAndDenyCounts()
    {
        var subjectId = UniqueSubjectId();
        var client = CreateClient("""{"evaluations":[{"decision":true},{"decision":false},{"decision":false}]}""");
        using var spans = new AuthZenSpanRecorder("authz.evaluate.batch",
            span => span.Events.Any(e => Equals(e.Tags.FirstOrDefault(t => t.Key == "authz.subject.id").Value, subjectId)));

        await client.AreAllowedAsync(Batch(subjectId, "read", "expense-1", "expense-2", "expense-3"));

        var span = spans.Single();
        span.GetTagItem("authz.batch.permits").Should().Be(1);
        span.GetTagItem("authz.batch.denies").Should().Be(2);
    }

    [Fact]
    public async Task AreAllowedAsync_PdpReturnsFewerDecisionsThanRequested_ThrowsAndCountsError()
    {
        var action = UniqueAction();
        var client = CreateClient("""{"evaluations":[{"decision":true}]}""");
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.AreAllowedAsync(Batch(UniqueSubjectId(), action, "expense-1", "expense-2"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        errors.Measurements.Should().ContainSingle()
            .Which.Tags.Should().Contain("operation", "evaluate.batch");
    }

    private static string UniqueSubjectId() => $"u-test-{Guid.NewGuid():N}";

    private static string UniqueAction() => $"test-action-{Guid.NewGuid():N}";

    private static AuthZenPolicyDecisionClient CreateClient(string pdpBody) =>
        CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(pdpBody, Encoding.UTF8, "application/json")
        });

    private static AuthZenPolicyDecisionClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http = FakeHttpMessageHandler.CreateClient(responder, out _);
        return new AuthZenPolicyDecisionClient(http, NullLogger<AuthZenPolicyDecisionClient>.Instance);
    }

    private static AccessEvaluationRequest Request(string subjectId, string action = "read") =>
        RequestFactory.ExpenseRequest(subjectId, action, ownerId: subjectId, status: "Draft");

    private static AccessEvaluationsRequest Batch(string subjectId, string action, params string[] resourceIds) =>
        new()
        {
            Subject = new Subject { Type = "user", Id = subjectId },
            Action = new AuthZenAction { Name = action },
            Evaluations = resourceIds
                .Select(id => new EvaluationEntry { Resource = new Resource { Type = "expense", Id = id } })
                .ToList()
        };
}
