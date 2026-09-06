using System.Net;
using System.Net.Http.Json;
using Meridian.DataAccess.Models;
using Meridian.Services.DTOs;

namespace Meridian.IntegrationTests.TestSupport;

// Swapped in for ExpensesLookupClient's real HttpClient in ReceiptsApiFactory.
// Upload eligibility needs the parent expense's owner/status, and standing up
// a full in-process Expenses.Api host (its own database, its own PDP wiring)
// just to serve that one lookup would be a much larger integration surface
// than what's actually under test here (the "receipt","create" PDP round
// trip) — so this returns a small fixed set of known expenses by id instead,
// the same way FakeReceiptBlobStorage stands in for real Azure Blob storage.
internal sealed class StubExpensesLookupHandler : HttpMessageHandler
{
    public static readonly Guid DraftExpenseId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    public static readonly Guid SubmittedExpenseId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    public const string ExpenseOwnerId = "u-emma";

    private static readonly Dictionary<Guid, ExpenseDto> Expenses = new()
    {
        [DraftExpenseId] = BuildExpense(DraftExpenseId, ExpenseStatus.Draft),
        [SubmittedExpenseId] = BuildExpense(SubmittedExpenseId, ExpenseStatus.Submitted)
    };

    private static ExpenseDto BuildExpense(Guid id, ExpenseStatus status) => new(
        Id: id,
        OwnerUserId: ExpenseOwnerId,
        Department: "Sales",
        Amount: 100m,
        Currency: "USD",
        Category: "Travel",
        Status: status,
        ApproverUserId: null,
        CreatedAt: DateTimeOffset.UtcNow,
        DecidedAt: null,
        RejectionReason: null);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = request.RequestUri!.AbsolutePath.TrimStart('/').Split('/');
        if (segments is ["expenses", var idSegment] && Guid.TryParse(idSegment, out var id)
            && Expenses.TryGetValue(id, out var expense))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expense)
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
