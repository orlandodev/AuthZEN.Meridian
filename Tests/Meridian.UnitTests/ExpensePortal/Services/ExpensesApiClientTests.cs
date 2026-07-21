using System.Net;
using Meridian.ExpensePortal.Models;
using Meridian.ExpensePortal.Services;
using Meridian.UnitTests.ExpensePortal.TestSupport;

namespace Meridian.UnitTests.ExpensePortal.Services;

public class ExpensesApiClientTests
{
    private static readonly Guid ExpenseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string OwnerUserId = "u-emma";
    private const string Department = "Sales";
    private const string Category = "Meals";
    private const decimal Amount = 42.50m;

    private static string ExpenseJson(Guid id) => $$"""
        {
            "id": "{{id}}",
            "ownerUserId": "{{OwnerUserId}}",
            "department": "{{Department}}",
            "amount": {{Amount}},
            "currency": "USD",
            "category": "{{Category}}",
            "status": 1,
            "approverUserId": null,
            "createdAt": "2026-01-01T00:00:00+00:00",
            "decidedAt": null
        }
        """;

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task GetMyExpensesAsync_DeserializesListFromResponse()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, $"[{ExpenseJson(ExpenseId)}]"), out _);
        var sut = new ExpensesApiClient(client);

        var result = await sut.GetMyExpensesAsync();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(ExpenseId);
        result[0].OwnerUserId.Should().Be(OwnerUserId);
        result[0].Status.Should().Be(ExpenseStatus.Submitted);
    }

    [Fact]
    public async Task GetMyExpensesAsync_ReturnsEmptyList_WhenBodyIsNull()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, "null"), out _);
        var sut = new ExpensesApiClient(client);

        var result = await sut.GetMyExpensesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExpenseAsync_ReturnsNull_WhenNotFound()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);
        var sut = new ExpensesApiClient(client);

        var result = await sut.GetExpenseAsync(ExpenseId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExpenseAsync_ReturnsDto_WhenFound()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, ExpenseJson(ExpenseId)), out _);
        var sut = new ExpensesApiClient(client);

        var result = await sut.GetExpenseAsync(ExpenseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(ExpenseId);
    }

    [Fact]
    public async Task GetExpenseAsync_Throws_OnServerError()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);
        var sut = new ExpensesApiClient(client);

        var act = async () => await sut.GetExpenseAsync(ExpenseId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateExpenseAsync_PostsRequest_AndReturnsCreatedDto()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, ExpenseJson(ExpenseId)), out var handler);
        var sut = new ExpensesApiClient(client);

        var (success, expense, error) = await sut.CreateExpenseAsync(new CreateExpenseRequest(Amount, Category));

        success.Should().BeTrue();
        error.Should().BeNull();
        expense!.Id.Should().Be(ExpenseId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/expenses");
    }

    [Fact]
    public async Task CreateExpenseAsync_ReturnsFailure_WithBody_OnBadRequest()
    {
        const string errorBody = "Amount must be between 0.01 and 1000000.";
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(errorBody) },
            out _);
        var sut = new ExpensesApiClient(client);

        var (success, expense, error) = await sut.CreateExpenseAsync(new CreateExpenseRequest(Amount, Category));

        success.Should().BeFalse();
        expense.Should().BeNull();
        error.Should().Be(errorBody);
    }

    [Fact]
    public async Task UpdateExpenseStatusAsync_PutsStatusRoute_AndReturnsSuccess_OnOk()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK), out var handler);
        var sut = new ExpensesApiClient(client);

        var (success, error) = await sut.UpdateExpenseStatusAsync(ExpenseId, ExpenseStatus.Approved);

        success.Should().BeTrue();
        error.Should().BeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be($"/expenses/{ExpenseId}/status");
    }

    [Fact]
    public async Task UpdateExpenseStatusAsync_ReturnsFriendlyMessage_OnForbidden()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.Forbidden), out _);
        var sut = new ExpensesApiClient(client);

        var (success, error) = await sut.UpdateExpenseStatusAsync(ExpenseId, ExpenseStatus.Rejected);

        success.Should().BeFalse();
        error.Should().Be("You're not authorized to decide this expense.");
    }

    [Fact]
    public async Task UpdateExpenseStatusAsync_ReturnsBody_WhenFailureBodyIsPresent()
    {
        const string errorBody = "Only a Submitted expense can be decided.";
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(errorBody) },
            out _);
        var sut = new ExpensesApiClient(client);

        var (success, error) = await sut.UpdateExpenseStatusAsync(ExpenseId, ExpenseStatus.Approved);

        success.Should().BeFalse();
        error.Should().Be(errorBody);
    }

    [Fact]
    public async Task UpdateExpenseStatusAsync_FallsBackToReasonPhrase_WhenBodyIsEmpty()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { ReasonPhrase = "Bad Gateway" },
            out _);
        var sut = new ExpensesApiClient(client);

        var (success, error) = await sut.UpdateExpenseStatusAsync(ExpenseId, ExpenseStatus.Approved);

        success.Should().BeFalse();
        error.Should().Be("Bad Gateway");
    }
}
