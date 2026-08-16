using System.Net;
using System.Net.Http.Headers;
using Meridian.ExpensePortal.Controllers;
using Meridian.ExpensePortal.Models;
using Meridian.ExpensePortal.Services;
using Meridian.UnitTests.ExpensePortal.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Meridian.UnitTests.ExpensePortal.Controllers;

// ReceiptsApiClient is sealed with no virtual members, so it can't be Moq'd directly —
// the controller is exercised against a real client backed by a FakeHttpMessageHandler,
// same approach as ExpensesControllerTests.
public class ReceiptsControllerTests
{
    private static readonly Guid ExpenseId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ReceiptId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static string ReceiptJson(Guid id, Guid expenseId) => $$"""
        {
            "id": "{{id}}", "expenseId": "{{expenseId}}", "fileName": "receipt.jpg",
            "contentType": "image/jpeg", "uploadedAt": "2026-01-01T00:00:00+00:00"
        }
        """;

    // Draft + owned by the test's caller — irrelevant to most tests here (only
    // ForExpense calls this), but must be a well-formed ExpenseDto so
    // ForExpense's expense lookup succeeds by default.
    private static string ExpenseJson(Guid id) => $$"""
        {
            "id": "{{id}}", "ownerUserId": "u-emma", "department": "Sales",
            "amount": 100, "currency": "USD", "category": "Travel",
            "status": 0, "approverUserId": null,
            "createdAt": "2026-01-01T00:00:00+00:00", "decidedAt": null
        }
        """;

    private static IFormFile BuildFormFile()
    {
        var bytes = "fake-file-content"u8.ToArray();
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "receipt.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private static ReceiptsController BuildController(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        Func<HttpRequestMessage, HttpResponseMessage>? expensesResponder = null)
    {
        var client = FakeHttpMessageHandler.CreateClient(responder, out _);
        var expensesClient = FakeHttpMessageHandler.CreateClient(
            expensesResponder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ExpenseJson(ExpenseId))
            }),
            out _);
        var controller = new ReceiptsController(new ReceiptsApiClient(client), new ExpensesApiClient(expensesClient));
        // ForExpense reads User.FindFirst(...) directly, so ControllerContext.HttpContext
        // (null by default in a bare controller instance) needs a real value here.
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task ForExpense_ReturnsViewWithReceiptsFromApi()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"[{ReceiptJson(ReceiptId, ExpenseId)}]")
        });

        var result = await sut.ForExpense(ExpenseId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeAssignableTo<List<ReceiptDto>>().Subject;
        model.Should().ContainSingle().Which.Id.Should().Be(ReceiptId);
    }

    [Fact]
    public async Task Upload_OnSuccess_RedirectsToForExpense_WithoutSettingError()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ReceiptJson(ReceiptId, ExpenseId))
        });

        var result = await sut.Upload(ExpenseId, BuildFormFile());

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceiptsController.ForExpense));
        redirect.RouteValues!["expenseId"].Should().Be(ExpenseId);
        sut.TempData.Should().NotContainKey("Error");
    }

    [Fact]
    public async Task Upload_OnApiFailure_SetsErrorMessage_AndStillRedirects()
    {
        const string errorBody = "Unsupported file type.";
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorBody)
        });

        var result = await sut.Upload(ExpenseId, BuildFormFile());

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ReceiptsController.ForExpense));
        sut.TempData["Error"].Should().Be(errorBody);
    }

    [Fact]
    public async Task Upload_WithNoFile_SetsErrorMessage_AndDoesNotCallApi()
    {
        var sut = BuildController(_ => throw new InvalidOperationException("API should not be called."));
        var emptyFile = new FormFile(new MemoryStream(), 0, 0, "file", "empty.txt");

        var result = await sut.Upload(ExpenseId, emptyFile);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ReceiptsController.ForExpense));
        sut.TempData["Error"].Should().Be("Choose a file to upload.");
    }

    [Fact]
    public async Task Download_OnSuccess_ReturnsFileResult()
    {
        var sut = BuildController(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("file-bytes"u8.ToArray())
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return response;
        });

        var result = await sut.Download(ReceiptId, ExpenseId);

        result.Should().BeOfType<FileStreamResult>().Which.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Download_OnForbidden_SetsErrorMessage_AndRedirectsToForExpense_WhenExpenseIdKnown()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.Download(ReceiptId, ExpenseId);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ReceiptsController.ForExpense));
        redirect.RouteValues!["expenseId"].Should().Be(ExpenseId);
        sut.TempData["Error"].Should().Be("You're not authorized to view this receipt.");
    }

    [Fact]
    public async Task Download_OnForbidden_RedirectsToExpensesIndex_WhenExpenseIdUnknown()
    {
        var sut = BuildController(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.Download(ReceiptId, expenseId: null);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ExpensesController.Index));
        redirect.ControllerName.Should().Be("Expenses");
    }
}
