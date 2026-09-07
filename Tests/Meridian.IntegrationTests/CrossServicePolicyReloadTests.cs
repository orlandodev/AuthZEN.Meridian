using System.Net;
using Meridian.DataAccess.PdP;
using Meridian.IntegrationTests.TestSupport;
using Meridian.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Meridian.IntegrationTests;

// One edit to the shared PDP's policy data, three APIs updated with no redeploy:
// flipping u-nadia from manager to employee in RoleAssignments turns her 200s
// into 403s on Expenses.Api, Receipts.Api and Reporting.Api on the very next
// request, then back again when the row is restored.
public class CrossServicePolicyReloadTests(CrossServicePolicyFixture fixture)
    : IClassFixture<CrossServicePolicyFixture>
{
    private static readonly Guid EmmaSubmittedExpenseId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid EmmaReceiptId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task PolicyDataEdit_TakesEffectAcrossAllThreeApis_WithNoRedeploy()
    {
        var expenses = CreateClient(fixture.Expenses, "u-nadia", Roles.Manager, "Sales");
        var receipts = CreateClient(fixture.Receipts, "u-nadia", Roles.Manager, "Sales");
        var reporting = CreateClient(fixture.Reporting, "u-nadia", Roles.Manager, "Sales");

        (await expenses.GetAsync($"/expenses/{EmmaSubmittedExpenseId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await receipts.GetAsync($"/receipts/{EmmaReceiptId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await reporting.GetAsync("/reports/department-spend")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        try
        {
            await fixture.SetManagerRoleAsync(PolicyConstants.RoleNames.Employee);

            (await expenses.GetAsync($"/expenses/{EmmaSubmittedExpenseId}")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await receipts.GetAsync($"/receipts/{EmmaReceiptId}")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await reporting.GetAsync("/reports/department-spend")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await fixture.SetManagerRoleAsync(PolicyConstants.RoleNames.Manager);
        }

        (await expenses.GetAsync($"/expenses/{EmmaSubmittedExpenseId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    private static HttpClient CreateClient<TEntryPoint>(
        WebApplicationFactory<TEntryPoint> factory, string userId, string role, string department)
        where TEntryPoint : class
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.DepartmentHeader, department);
        return client;
    }
}
