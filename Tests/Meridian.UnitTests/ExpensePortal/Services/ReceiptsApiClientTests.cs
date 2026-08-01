using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Meridian.ExpensePortal.Services;
using Meridian.UnitTests.ExpensePortal.TestSupport;
using Microsoft.AspNetCore.Http;

namespace Meridian.UnitTests.ExpensePortal.Services;

public class ReceiptsApiClientTests
{
    private static readonly Guid ExpenseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ReceiptId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static string ReceiptJson(Guid id, Guid expenseId) => $$"""
        {
            "id": "{{id}}",
            "expenseId": "{{expenseId}}",
            "fileName": "receipt.jpg",
            "contentType": "image/jpeg",
            "uploadedAt": "2026-01-01T00:00:00+00:00"
        }
        """;

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static IFormFile BuildFormFile(string fileName = "receipt.jpg", string contentType = "image/jpeg")
    {
        var bytes = "fake-file-content"u8.ToArray();
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task GetReceiptsForExpenseAsync_DeserializesListFromResponse_AndQueriesByExpenseId()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, $"[{ReceiptJson(ReceiptId, ExpenseId)}]"), out var handler);
        var sut = new ReceiptsApiClient(client);

        var result = await sut.GetReceiptsForExpenseAsync(ExpenseId);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(ReceiptId);
        result[0].FileName.Should().Be("receipt.jpg");
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/receipts?expenseId={ExpenseId}");
    }

    [Fact]
    public async Task GetReceiptsForExpenseAsync_ReturnsEmptyList_WhenBodyIsNull()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, "null"), out _);
        var sut = new ReceiptsApiClient(client);

        var result = await sut.GetReceiptsForExpenseAsync(ExpenseId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadReceiptAsync_PostsMultipartRequest_AndReturnsCreatedDto()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => JsonResponse(HttpStatusCode.OK, ReceiptJson(ReceiptId, ExpenseId)), out var handler);
        var sut = new ReceiptsApiClient(client);

        var (success, receipt, error) = await sut.UploadReceiptAsync(ExpenseId, BuildFormFile());

        success.Should().BeTrue();
        error.Should().BeNull();
        receipt!.Id.Should().Be(ReceiptId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/receipts");
        handler.LastRequest.Content.Should().BeOfType<MultipartFormDataContent>();
    }

    [Fact]
    public async Task UploadReceiptAsync_ReturnsFailure_WithBody_OnBadRequest()
    {
        const string errorBody = "Unsupported file type.";
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(errorBody) },
            out _);
        var sut = new ReceiptsApiClient(client);

        var (success, receipt, error) = await sut.UploadReceiptAsync(ExpenseId, BuildFormFile());

        success.Should().BeFalse();
        receipt.Should().BeNull();
        error.Should().Be(errorBody);
    }

    [Fact]
    public async Task DownloadReceiptAsync_ReturnsContentAndContentType_OnSuccess()
    {
        var client = FakeHttpMessageHandler.CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("file-bytes"u8.ToArray())
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return response;
        }, out _);
        var sut = new ReceiptsApiClient(client);

        var (content, contentType, error) = await sut.DownloadReceiptAsync(ReceiptId);

        content.Should().NotBeNull();
        contentType.Should().Be("image/jpeg");
        error.Should().BeNull();
    }

    [Fact]
    public async Task DownloadReceiptAsync_ReturnsFriendlyMessage_OnForbidden()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.Forbidden), out _);
        var sut = new ReceiptsApiClient(client);

        var (content, _, error) = await sut.DownloadReceiptAsync(ReceiptId);

        content.Should().BeNull();
        error.Should().Be("You're not authorized to view this receipt.");
    }

    [Fact]
    public async Task DownloadReceiptAsync_ReturnsFriendlyMessage_OnNotFound()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound), out _);
        var sut = new ReceiptsApiClient(client);

        var (content, _, error) = await sut.DownloadReceiptAsync(ReceiptId);

        content.Should().BeNull();
        error.Should().Be("Receipt not found.");
    }

    [Fact]
    public async Task DownloadReceiptAsync_Throws_OnServerError()
    {
        var client = FakeHttpMessageHandler.CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);
        var sut = new ReceiptsApiClient(client);

        var act = async () => await sut.DownloadReceiptAsync(ReceiptId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
