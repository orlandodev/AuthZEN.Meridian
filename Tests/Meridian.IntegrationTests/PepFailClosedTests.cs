using System.Diagnostics.Metrics;
using System.Net;
using AuthZen.Pep;
using Meridian.IntegrationTests.TestSupport;
using Meridian.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meridian.IntegrationTests;

// Proves the PEP fails closed: when Expenses.Api can't get a decision from
// the PDP, the caller gets a 500 — never the resource — and the failure is
// counted in authz.pep.errors. The PDP here rejects the PEP's token (401),
// which the standard resilience handler doesn't retry, so the test stays fast;
// a 5xx outage takes the same path after the retries are exhausted.
public class PepFailClosedTests(ExpensesPdpFixture fixture) : IClassFixture<ExpensesPdpFixture>
{
    private static readonly Guid EmmaSubmittedExpenseId = Guid.Parse("e0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Read_PdpRejectsPepToken_Returns500AndCountsPepError()
    {
        using var factory = fixture.Expenses.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPolicyDecisionClient>();
                services.AddHttpClient<IPolicyDecisionClient, AuthZenPolicyDecisionClient>(client =>
                        client.BaseAddress = new Uri("http://pdp-test/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StatusCodeHandler(HttpStatusCode.Unauthorized));
            }));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.UserIdHeader, "u-emma");
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.RoleHeader, Roles.Employee);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.DepartmentHeader, "Sales");
        var errorTypes = new List<object?>();
        using var listener = ListenForPepErrors(errorTypes);

        var response = await client.GetAsync($"/expenses/{EmmaSubmittedExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        lock (errorTypes)
        {
            errorTypes.Should().ContainSingle().Which.Should().Be("401");
        }
    }

    // No other test in the suite makes the PDP answer 401, so any 401-tagged
    // authz.pep.errors measurement here belongs to this test.
    private static MeterListener ListenForPepErrors(List<object?> errorTypes)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == "Meridian.AuthZen" && instrument.Name == "authz.pep.errors")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "error.type" && Equals(tag.Value, "401"))
                {
                    lock (errorTypes)
                    {
                        errorTypes.Add(tag.Value);
                    }
                }
            }
        });
        listener.Start();
        return listener;
    }
}
