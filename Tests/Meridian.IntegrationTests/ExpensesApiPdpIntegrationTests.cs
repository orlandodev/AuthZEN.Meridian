using System.Net;
using System.Net.Http.Json;
using Meridian.DataAccess.Models;
using Meridian.IntegrationTests.TestSupport;
using Meridian.Services;
using Meridian.Services.DTOs;

namespace Meridian.IntegrationTests;

// End-to-end proof that Expenses.Api's Stage 3 conversion works over the
// wire: real handlers/filter -> AuthZenPolicyDecisionClient -> HTTP ->
// Pdp.Service -> PolicyRulesEngine -> decision back. Unit tests already cover
// each half in isolation with mocks; this is the seam those can't verify.
//
// Ids e0000000-...-0001/2/3 come from ExpensesDbContext's HasData (read-only
// here); f0000000-...-0001/2/3 come from ExpensesPdpFixture, dedicated to the
// mutating approve/reject tests so they can't interfere with each other.
public class ExpensesApiPdpIntegrationTests(ExpensesPdpFixture fixture) : IClassFixture<ExpensesPdpFixture>
{
    private static readonly Guid EmmaSubmittedExpenseId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid EmmaDraftExpenseId = Guid.Parse("e0000000-0000-0000-0000-000000000002");

    private HttpClient CreateClient(string userId, string role, string? department = null)
    {
        var client = fixture.Expenses.CreateClient();
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.RoleHeader, role);
        if (department is not null)
        {
            client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.DepartmentHeader, department);
        }
        return client;
    }

    [Fact]
    public async Task Create_AsEmployee_Returns201()
    {
        var client = CreateClient("u-emma", Roles.Employee, "Sales");

        var response = await client.PostAsJsonAsync("/expenses", new CreateExpenseRequest(50m, "Meals"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithoutDepartmentClaim_Returns400()
    {
        var client = CreateClient("u-ghost", Roles.Employee, department: null);

        var response = await client.PostAsJsonAsync("/expenses", new CreateExpenseRequest(50m, "Meals"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Read_OwnExpense_Returns200()
    {
        var client = CreateClient("u-emma", Roles.Employee, "Sales");

        var response = await client.GetAsync($"/expenses/{EmmaSubmittedExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Read_AnotherEmployeesSubmittedExpense_Returns403()
    {
        // u-mateo is a stranger to u-emma's expense: no ownership, no role, no relation.
        var client = CreateClient("u-mateo", Roles.Employee, "Sales");

        var response = await client.GetAsync($"/expenses/{EmmaSubmittedExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Read_ManagerReadsSubmittedExpense_Returns200()
    {
        var client = CreateClient("u-nadia", Roles.Manager, "Sales");

        var response = await client.GetAsync($"/expenses/{EmmaSubmittedExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Read_ManagerReadsDraftExpense_Returns403()
    {
        // The Draft carve-out, proven over real HTTP through both services.
        var client = CreateClient("u-nadia", Roles.Manager, "Sales");

        var response = await client.GetAsync($"/expenses/{EmmaDraftExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_ManagerUnderLimit_Returns200()
    {
        var client = CreateClient("u-nadia", Roles.Manager, "Sales");

        var response = await client.PutAsJsonAsync(
            $"/expenses/{ExpensesPdpFixture.ApproveUnderLimitExpenseId}/status",
            new UpdateExpenseStatusRequest(ExpenseStatus.Approved));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Approve_ManagerOverLimit_Returns403()
    {
        var client = CreateClient("u-nadia", Roles.Manager, "Sales");

        var response = await client.PutAsJsonAsync(
            $"/expenses/{ExpensesPdpFixture.ApproveOverLimitExpenseId}/status",
            new UpdateExpenseStatusRequest(ExpenseStatus.Approved));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_Finance_AnyAmount_Returns200()
    {
        var client = CreateClient("u-finn", Roles.Finance, "Finance");

        var response = await client.PutAsJsonAsync(
            $"/expenses/{ExpensesPdpFixture.FinanceApprovesAnyAmountExpenseId}/status",
            new UpdateExpenseStatusRequest(ExpenseStatus.Rejected));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
