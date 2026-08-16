using System.Net;
using Meridian.Receipts.Api.Services;
using Meridian.UnitTests.ExpensePortal.TestSupport;

namespace Meridian.UnitTests.ReceiptsApi.Services;

public class ExpensesLookupClientTests
{
    private static readonly Guid ExpenseId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private static string ExpenseJson(Guid id) => $$"""
        {
            "id": "{{id}}", "ownerUserId": "u-emma", "department": "Sales",
            "amount": 100, "currency": "USD", "category": "Travel",
            "status": 0, "approverUserId": null,
            "createdAt": "2026-01-01T00:00:00+00:00", "decidedAt": null
        }
        """;

    [Fact]
    public async Task GetExpenseAsync_ReturnsExpense_OnSuccess()
    {
        var client = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ExpenseJson(ExpenseId))
        }, out _);
        var sut = new ExpensesLookupClient(client);

        var result = await sut.GetExpenseAsync(ExpenseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(ExpenseId);
    }

    [Fact]
    public async Task GetExpenseAsync_ReturnsNull_WhenNotFound()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);
        var sut = new ExpensesLookupClient(client);

        (await sut.GetExpenseAsync(ExpenseId)).Should().BeNull();
    }

    [Fact]
    public async Task GetExpenseAsync_ReturnsNull_WhenForbidden()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.Forbidden), out _);
        var sut = new ExpensesLookupClient(client);

        (await sut.GetExpenseAsync(ExpenseId)).Should().BeNull();
    }

    // A transient Expenses.Api failure must not be silently reported to the
    // caller as "not authorized" — it should surface as a real failure instead
    // of collapsing into the same null the NotFound/Forbidden cases return.
    [Fact]
    public async Task GetExpenseAsync_Throws_OnServerError()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);
        var sut = new ExpensesLookupClient(client);

        var act = async () => await sut.GetExpenseAsync(ExpenseId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
