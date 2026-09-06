using System.Net;
using System.Net.Http.Json;
using Meridian.IntegrationTests.TestSupport;
using Meridian.Services;
using Meridian.Services.DTOs;

namespace Meridian.IntegrationTests;

// End-to-end proof that Reporting.Api's PEP conversion works over the wire,
// for both PDP-backed decisions it converts: the real
// DepartmentSpendReadFilter/DepartmentSpendExportFilter ->
// AuthZenPolicyDecisionClient -> HTTP -> Pdp.Service -> PolicyRulesEngine ->
// decision back. Unit tests already cover each half in isolation with mocks;
// this is the seam those can't verify — in particular the payoff of the
// story: the Monday-Friday 9am-5pm export window, once a C# `if` inside the
// endpoint, is now the PDP's DepartmentSpendRules.CanExport, decided against
// the PDP's own clock in the configured business zone (pinned by
// ReportingPdpFixture).
//
// Subjects come from the PDP's own seed data (see RulesEngineTests):
// u-finn is Finance, u-nadia manages Sales, u-emma is a Sales employee.
public class ReportingApiPdpIntegrationTests(ReportingPdpFixture fixture) : IClassFixture<ReportingPdpFixture>
{
    private static HttpClient CreateClient(ReportingApiFactory factory, string userId, string role, string department)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.DepartmentHeader, department);
        return client;
    }

    [Fact]
    public async Task DepartmentSpend_Finance_Returns200_WithEveryDepartment()
    {
        var client = CreateClient(fixture.Reporting, "u-finn", Roles.Finance, "Finance");

        var response = await client.GetAsync("/reports/department-spend");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await response.Content.ReadFromJsonAsync<List<DepartmentSpendSummaryDto>>();
        summaries.Should().Contain(s => s.Department == "Sales")
            .And.Contain(s => s.Department == "Finance");
    }

    [Fact]
    public async Task DepartmentSpend_ManagerOfOwnDepartment_Returns200_ScopedToThatDepartment()
    {
        var client = CreateClient(fixture.Reporting, "u-nadia", Roles.Manager, "Sales");

        var response = await client.GetAsync("/reports/department-spend");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await response.Content.ReadFromJsonAsync<List<DepartmentSpendSummaryDto>>();
        summaries.Should().OnlyContain(s => s.Department == "Sales");
    }

    [Fact]
    public async Task DepartmentSpend_Employee_Returns403()
    {
        var client = CreateClient(fixture.Reporting, "u-emma", Roles.Employee, "Sales");

        var response = await client.GetAsync("/reports/department-spend");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DepartmentSpend_ManagerWithMismatchedDepartmentClaim_Returns403()
    {
        // The PDP scopes a manager against the department on its own
        // RoleAssignment row (Sales), not the claim the PEP forwards — so a
        // manager who tampers their token's department claim still can't read
        // another department's spend.
        var client = CreateClient(fixture.Reporting, "u-nadia", Roles.Manager, "Finance");

        var response = await client.GetAsync("/reports/department-spend");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_FinanceWithinBusinessHours_Returns200_AsCsv()
    {
        var client = CreateClient(fixture.Reporting, "u-finn", Roles.Finance, "Finance");

        var response = await client.GetAsync("/reports/department-spend/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    [Fact]
    public async Task Export_FinanceOutsideBusinessHours_Returns403()
    {
        // The regression this story's payoff protects: the same Finance caller
        // who succeeds inside the window is denied outside it — and the window
        // is now the PDP's DepartmentSpendRules.CanExport, not a C# `if` in the
        // endpoint. ReportingOutsideHours is chained to a PDP whose clock is
        // pinned to a Saturday.
        var client = CreateClient(fixture.ReportingOutsideHours, "u-finn", Roles.Finance, "Finance");

        var response = await client.GetAsync("/reports/department-spend/export");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_ManagerWithinBusinessHours_Returns403()
    {
        // Export is Finance-only, even inside the window and even for the
        // manager's own department.
        var client = CreateClient(fixture.Reporting, "u-nadia", Roles.Manager, "Sales");

        var response = await client.GetAsync("/reports/department-spend/export");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
