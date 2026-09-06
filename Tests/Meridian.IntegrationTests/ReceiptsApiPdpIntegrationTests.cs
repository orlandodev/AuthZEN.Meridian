using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Meridian.IntegrationTests.TestSupport;
using Meridian.Services;
using Meridian.Services.DTOs;

namespace Meridian.IntegrationTests;

// End-to-end proof that Receipts.Api's PEP conversion works over the wire, for
// both PDP-backed decisions it converts: the real
// OwnerOrPrivilegedHandler/UploadEligibilityHandler -> AuthZenPolicyDecisionClient
// -> HTTP -> Pdp.Service -> PolicyRulesEngine -> decision back. Unit tests
// already cover each half in isolation with mocks; this is the seam those
// can't verify — including the regression it fixes: a manager who used to get
// 403 downloading a report's receipt (Receipts.Api's own
// OwnerOrPrivilegedHandler never had a manager-of branch) now succeeds,
// because ReceiptRules.CanRead does have one.
//
// Ids b0000000-...-0001/2 come from ReceiptsDbContext's HasData (owned by
// u-emma and u-mateo respectively); u-nadia manages both per the PDP's own
// seed data (see RulesEngineTests). The Upload_* tests below authorize
// against StubExpensesLookupHandler's own fixed expenses (also owned by
// u-emma) instead, since upload eligibility is checked against the parent
// Expense, not a Receipt.
public class ReceiptsApiPdpIntegrationTests(ReceiptsPdpFixture fixture) : IClassFixture<ReceiptsPdpFixture>
{
    private static readonly Guid EmmaReceiptId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid EmmaExpenseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private HttpClient CreateClient(string userId, string role, string? department = null)
    {
        var client = fixture.Receipts.CreateClient();
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.RoleHeader, role);
        if (department is not null)
        {
            client.DefaultRequestHeaders.Add(EndUserTestAuthHandler.DepartmentHeader, department);
        }
        return client;
    }

    [Fact]
    public async Task Download_Owner_Returns200()
    {
        var client = CreateClient("u-emma", Roles.Employee, "Sales");

        var response = await client.GetAsync($"/receipts/{EmmaReceiptId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ManagerOfOwner_Returns200()
    {
        // The regression this story exists to fix: Receipts.Api's own
        // in-process check had no manager-of branch; the PDP's
        // ReceiptRules.CanRead does, so this now succeeds instead of
        // 403 — over real HTTP, not a mock.
        var client = CreateClient("u-nadia", Roles.Manager, "Sales");

        var response = await client.GetAsync($"/receipts/{EmmaReceiptId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_Finance_Returns200()
    {
        var client = CreateClient("u-finn", Roles.Finance, "Finance");

        var response = await client.GetAsync($"/receipts/{EmmaReceiptId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_KnownNonOwnerEmployee_Returns403()
    {
        // u-mateo is a real, seeded employee — not a manager of u-emma, not
        // Finance, not the owner — so ReceiptRules.CanRead denies.
        var client = CreateClient("u-mateo", Roles.Employee, "Sales");

        var response = await client.GetAsync($"/receipts/{EmmaReceiptId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Download_UnknownSubject_Returns403()
    {
        // u-ghost has no RoleAssignment row at all in the PDP's policy
        // database, so the subject-profile lookup misses entirely — denied,
        // not treated as equivalent to any known role.
        var client = CreateClient("u-ghost", Roles.Employee, "Sales");

        var response = await client.GetAsync($"/receipts/{EmmaReceiptId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ManagerOfOwner_IncludesTheirReceipt()
    {
        // The list-endpoint half of the same regression Download_ManagerOfOwner_Returns200
        // fixes: before ReceiptVisibilityFilter, GetForExpenseAsync's manager branch fell
        // through to owner-only, so a manager who could now download a receipt directly by
        // id still couldn't discover it through the list the Portal's "Receipts" link uses.
        var client = CreateClient("u-nadia", Roles.Manager, "Sales");

        var response = await client.GetAsync($"/receipts?expenseId={EmmaExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipts = await response.Content.ReadFromJsonAsync<List<ReceiptDto>>();
        receipts.Should().ContainSingle(r => r.Id == EmmaReceiptId);
    }

    [Fact]
    public async Task List_Finance_IncludesEveryReceipt()
    {
        var client = CreateClient("u-finn", Roles.Finance, "Finance");

        var response = await client.GetAsync($"/receipts?expenseId={EmmaExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipts = await response.Content.ReadFromJsonAsync<List<ReceiptDto>>();
        receipts.Should().ContainSingle(r => r.Id == EmmaReceiptId);
    }

    [Fact]
    public async Task List_KnownNonOwnerEmployee_ExcludesOthersReceipts()
    {
        // u-mateo is neither the owner, Finance, nor a manager of u-emma — the
        // manager branch's widened candidate set (see ReceiptService.GetForExpenseAsync)
        // never applies to him, so this stays exactly as narrow as before.
        var client = CreateClient("u-mateo", Roles.Employee, "Sales");

        var response = await client.GetAsync($"/receipts?expenseId={EmmaExpenseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipts = await response.Content.ReadFromJsonAsync<List<ReceiptDto>>();
        receipts.Should().NotContain(r => r.Id == EmmaReceiptId);
    }

    private static MultipartFormDataContent BuildUploadContent(Guid expenseId)
    {
        var fileContent = new ByteArrayContent("fake-receipt-bytes"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent
        {
            { new StringContent(expenseId.ToString()), "expenseId" },
            { fileContent, "file", "receipt.png" }
        };
    }

    [Fact]
    public async Task Upload_OwnerOnDraftExpense_Returns201()
    {
        var client = CreateClient(StubExpensesLookupHandler.ExpenseOwnerId, Roles.Employee, "Sales");

        var response = await client.PostAsync("/receipts", BuildUploadContent(StubExpensesLookupHandler.DraftExpenseId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Upload_OwnerOnSubmittedExpense_Returns403()
    {
        // The regression this rule exists to prevent: upload closes once the
        // expense leaves Draft, even for its own owner — now enforced by the
        // PDP's ReceiptRules.CanCreate, proven here over real HTTP rather than
        // only against a mocked IPolicyDecisionClient.
        var client = CreateClient(StubExpensesLookupHandler.ExpenseOwnerId, Roles.Employee, "Sales");

        var response = await client.PostAsync("/receipts", BuildUploadContent(StubExpensesLookupHandler.SubmittedExpenseId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_NonOwnerOnDraftExpense_Returns403()
    {
        // Unlike Download, Upload gets no Finance/manager carve-out — only
        // the literal owner may ever upload, per ReceiptRules.CanCreate.
        var client = CreateClient("u-mateo", Roles.Employee, "Sales");

        var response = await client.PostAsync("/receipts", BuildUploadContent(StubExpensesLookupHandler.DraftExpenseId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
