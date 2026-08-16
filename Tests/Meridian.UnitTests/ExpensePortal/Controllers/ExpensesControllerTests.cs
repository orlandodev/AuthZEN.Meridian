using System.Net;
using Meridian.ExpensePortal.Controllers;
using Meridian.ExpensePortal.Models;
using Meridian.ExpensePortal.Services;
using Meridian.UnitTests.ExpensePortal.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Meridian.UnitTests.ExpensePortal.Controllers;

// ExpensesApiClient is sealed with no virtual members, so it can't be Moq'd
// directly — the controller is exercised against a real client backed by a
// FakeHttpMessageHandler instead.
public class ExpensesControllerTests
{
    private static readonly Guid ExpenseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Category = "Travel";
    private const decimal Amount = 100m;

    private static string ExpenseJson(Guid id) => $$"""
        {
            "id": "{{id}}", "ownerUserId": "u-emma", "department": "Sales",
            "amount": {{Amount}}, "currency": "USD", "category": "{{Category}}",
            "status": 0, "approverUserId": null,
            "createdAt": "2026-01-01T00:00:00+00:00", "decidedAt": null
        }
        """;

    private static ExpensesController BuildController(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = FakeHttpMessageHandler.CreateClient(responder, out _);
        var controller = new ExpensesController(new ExpensesApiClient(client));
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Index_ReturnsViewWithExpensesFromApi()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"[{ExpenseJson(ExpenseId)}]")
        });

        var result = await sut.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeAssignableTo<List<ExpenseDto>>().Subject;
        model.Should().ContainSingle().Which.Id.Should().Be(ExpenseId);
    }

    [Fact]
    public async Task Create_PostsRequest_AndRedirectsToIndex()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ExpenseJson(ExpenseId))
        });

        var result = await sut.Create(new CreateExpenseRequest(Amount, Category));

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData.Should().NotContainKey("Error");
    }

    [Fact]
    public async Task Create_OnApiFailure_SetsErrorMessage_AndStillRedirectsToIndex()
    {
        const string errorBody = "Amount must be between 0.01 and 1000000.";
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorBody)
        });

        var result = await sut.Create(new CreateExpenseRequest(Amount, Category));

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData["Error"].Should().Be(errorBody);
    }

    [Fact]
    public async Task Create_OnInvalidModelState_SetsErrorMessage_AndDoesNotCallApi()
    {
        var sut = BuildController(_ => throw new InvalidOperationException("API should not be called."));
        sut.ModelState.AddModelError(nameof(CreateExpenseRequest.Amount), "Amount is out of range.");

        var result = await sut.Create(new CreateExpenseRequest(-1m, Category));

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData["Error"].Should().Be("Please provide a valid amount and category.");
    }

    [Fact]
    public async Task Approve_OnSuccess_RedirectsToIndex_WithoutSettingError()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await sut.Approve(ExpenseId);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData.Should().NotContainKey("Error");
    }

    [Fact]
    public async Task Approve_OnForbidden_SetsErrorMessage_AndStillRedirectsToIndex()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.Approve(ExpenseId);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData["Error"].Should().Be("You're not authorized to decide this expense.");
    }

    [Fact]
    public async Task Reject_OnSuccess_RedirectsToIndex_WithoutSettingError()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await sut.Reject(ExpenseId, "Missing an itemized receipt.");

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData.Should().NotContainKey("Error");
    }

    [Fact]
    public async Task Reject_OnForbidden_SetsErrorMessage_AndStillRedirectsToIndex()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.Reject(ExpenseId, "Missing an itemized receipt.");

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData["Error"].Should().Be("You're not authorized to decide this expense.");
    }

    [Fact]
    public async Task Reject_WithNoReason_SetsErrorMessage_AndDoesNotCallApi()
    {
        var sut = BuildController(_ => throw new InvalidOperationException("API should not be called."));

        var result = await sut.Reject(ExpenseId, "   ");

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData["Error"].Should().Be("A reason is required when rejecting an expense.");
    }

    [Fact]
    public async Task Details_ReturnsViewWithExpense_WhenFound()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ExpenseJson(ExpenseId))
        });

        var result = await sut.Details(ExpenseId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeAssignableTo<ExpenseDto>().Which.Id.Should().Be(ExpenseId);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenExpenseDoesNotExist()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.Details(ExpenseId);

        result.Should().BeOfType<NotFoundResult>();
    }

    // Pins the drift bug's UI-reachable path: a manager viewing another
    // department's expense (or any other non-owner/non-privileged caller) hits
    // this 403 rather than an unhandled exception.
    [Fact]
    public async Task Details_OnForbidden_SetsErrorMessage_AndRedirectsToIndex()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.Details(ExpenseId);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ExpensesController.Index));
        sut.TempData["Error"].Should().Be("You're not authorized to view this expense.");
    }
}
