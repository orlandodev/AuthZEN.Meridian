using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthZen.Contracts;

namespace Meridian.UnitTests.PdpService;

public class PdpApiIntegrationTests(PdpApiFactory factory) : IClassFixture<PdpApiFactory>
{
    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", TestAuthHandler.SentinelHeaderValue);
        return client;
    }

    [Fact]
    public async Task Evaluation_WithoutAuth_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/access/v1/evaluation", RequestFactory.ExpenseRequest(
            "u-emma", "read", ownerId: "u-emma", status: "Submitted"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Evaluation_ManagerReadsSubmittedExpense_ReturnsDecisionTrue()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/access/v1/evaluation", RequestFactory.ExpenseRequest(
            "u-nadia", "read", ownerId: "u-emma", status: "Submitted"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccessEvaluationResponse>();
        body!.Decision.Should().BeTrue();
    }

    [Fact]
    public async Task Evaluation_ManagerReadsDraftExpense_ReturnsDecisionFalse()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/access/v1/evaluation", RequestFactory.ExpenseRequest(
            "u-nadia", "read", ownerId: "u-emma", status: "Draft"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccessEvaluationResponse>();
        body!.Decision.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluations_Boxcar_ReturnsPerItemDecisions()
    {
        var client = CreateAuthenticatedClient();

        // Top-level subject/action are batch defaults; only the first entry
        // overrides them, proving the boxcar defaults-merge works over real HTTP.
        var batch = new AccessEvaluationsRequest
        {
            Subject = new Subject { Type = "user", Id = "u-finn" },
            Action = new AuthZenAction { Name = "read" },
            Evaluations =
            [
                new EvaluationEntry
                {
                    Subject = new Subject { Type = "user", Id = "u-mateo" },
                    Resource = new Resource
                    {
                        Type = "expense",
                        Id = "e1",
                        Properties = new Dictionary<string, object> { ["ownerId"] = "u-emma", ["status"] = "Submitted" }
                    }
                },
                new EvaluationEntry
                {
                    Resource = new Resource
                    {
                        Type = "expense",
                        Id = "e2",
                        Properties = new Dictionary<string, object> { ["ownerId"] = "u-emma", ["status"] = "Submitted" }
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/access/v1/evaluations", batch);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccessEvaluationsResponse>();
        body!.Evaluations.Should().HaveCount(2);
        body.Evaluations[0].Decision.Should().BeFalse(); // u-mateo: stranger to u-emma's expense
        body.Evaluations[1].Decision.Should().BeTrue();  // inherits subject u-finn (finance) from batch default
    }

    [Fact]
    public async Task Evaluations_Boxcar_EntryMissingResourceAndNoBatchDefault_ReturnsBadRequest()
    {
        // Regression: this used to throw an unhandled InvalidOperationException
        // (raw 500) instead of a clean 400.
        var client = CreateAuthenticatedClient();

        var batch = new AccessEvaluationsRequest
        {
            Subject = new Subject { Type = "user", Id = "u-finn" },
            Action = new AuthZenAction { Name = "read" },
            Evaluations = [new EvaluationEntry()]
        };

        var response = await client.PostAsJsonAsync("/access/v1/evaluations", batch);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Discovery_IsAnonymous_AndMatchesSpecFieldSet()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/authzen-configuration");

        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();
        keys.Should().BeEquivalentTo(["access_evaluation_endpoint", "policy_decision_point"]);
    }
}
