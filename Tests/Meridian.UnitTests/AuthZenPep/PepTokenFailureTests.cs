using System.Net;
using System.Text;
using AuthZen.Pep;
using Meridian.UnitTests.ExpensePortal.TestSupport;
using Meridian.UnitTests.PdpService;
using Meridian.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.UnitTests.AuthZenPep;

// Drives the real AddAuthZenPep registration (Duende client-credentials
// token handling included) against a token endpoint that rejects the PEP's
// client, to pin down how a token failure surfaces: it must fail closed and
// land in authz.pep.errors like any other failure to reach a PDP decision.
public class PepTokenFailureTests
{
    private const string TokenEndpoint = "https://identityserver.test/connect/token";

    // Duende.AccessTokenManagement doesn't throw when the token request fails:
    // it sends the evaluation without a bearer token, so the failure surfaces
    // as the PDP's 401.
    [Fact]
    public async Task IsAllowedAsync_TokenEndpointRejectsClient_CountsErrorAsPdp401()
    {
        var action = $"test-action-{Guid.NewGuid():N}";
        await using var provider = BuildPep();
        var client = provider.GetRequiredService<IPolicyDecisionClient>();
        using var errors = new AuthZenMeasurementRecorder("authz.pep.errors", "action", action);

        var act = () => client.IsAllowedAsync(
            RequestFactory.ExpenseRequest("u-emma", action, ownerId: "u-emma", status: "Draft"));

        await act.Should().ThrowAsync<HttpRequestException>();
        errors.Measurements.Should().ContainSingle()
            .Which.Tags["error.type"].Should().Be("401");
    }

    private static ServiceProvider BuildPep()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthZenPep(
            pdpBaseAddress: "https://pdp.test/",
            identityServerTokenEndpoint: TokenEndpoint,
            clientId: "meridian.pep",
            clientSecret: "wrong-secret");
        services.ConfigureHttpClientDefaults(http =>
            http.ConfigurePrimaryHttpMessageHandler(() => new FakeHttpMessageHandler(Respond)));
        return services.BuildServiceProvider();
    }

    // The token endpoint rejects the client; the PDP, like the real one,
    // rejects any evaluation that arrives without a bearer token.
    private static HttpResponseMessage Respond(HttpRequestMessage request)
    {
        if (request.RequestUri!.ToString() == TokenEndpoint)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_client"}""", Encoding.UTF8, "application/json")
            };
        }

        return request.Headers.Authorization is null
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"decision":true}""", Encoding.UTF8, "application/json")
            };
    }
}
