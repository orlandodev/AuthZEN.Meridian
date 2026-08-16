using System.Net;
using Meridian.Expenses.Api.Services;
using Meridian.UnitTests.ExpensePortal.TestSupport;

namespace Meridian.UnitTests.ExpensesApi.Services;

public class ReceiptsLookupClientTests
{
    private static readonly Guid ExpenseId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static string ReceiptJson(Guid expenseId) => $$"""
        {
            "id": "{{Guid.NewGuid()}}", "expenseId": "{{expenseId}}", "ownerUserId": "u-emma",
            "fileName": "receipt.jpg", "contentType": "image/jpeg", "uploadedAt": "2026-01-01T00:00:00+00:00"
        }
        """;

    [Fact]
    public async Task HasAnyReceiptsAsync_ReturnsTrue_WhenAtLeastOneReceiptExists()
    {
        var client = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"[{ReceiptJson(ExpenseId)}]")
        }, out _);
        var sut = new ReceiptsLookupClient(client);

        (await sut.HasAnyReceiptsAsync(ExpenseId)).Should().BeTrue();
    }

    [Fact]
    public async Task HasAnyReceiptsAsync_ReturnsFalse_WhenNoReceiptsExist()
    {
        var client = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        }, out _);
        var sut = new ReceiptsLookupClient(client);

        (await sut.HasAnyReceiptsAsync(ExpenseId)).Should().BeFalse();
    }

    // Fails closed: Submit must not silently proceed when Receipts.Api can't be
    // reached or errors — see HasAnyReceiptsAsync's comment for why.
    [Fact]
    public async Task HasAnyReceiptsAsync_Throws_WhenReceiptsApiReturnsServerError()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);
        var sut = new ReceiptsLookupClient(client);

        var act = async () => await sut.HasAnyReceiptsAsync(ExpenseId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
