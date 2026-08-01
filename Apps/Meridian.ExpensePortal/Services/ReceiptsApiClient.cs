using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Meridian.ExpensePortal.Models;

namespace Meridian.ExpensePortal.Services;

// Typed client for the Receipts API. Token attachment happens via the access-token
// handler registered in Program.cs, so this class only knows about the Receipts
// API's shape — not about auth. Same JSON-casing caveat as
// ExpensesApiClient/ReportingApiClient: HttpClient's deserializer isn't camelCase by
// default, so JsonOptions is passed explicitly everywhere.
public sealed class ReceiptsApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<ReceiptDto>> GetReceiptsForExpenseAsync(Guid expenseId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ReceiptDto>>($"receipts?expenseId={expenseId}", JsonOptions, ct) ?? [];

    // Returns a friendly reason on failure instead of throwing, since a 400 here
    // (bad file, etc.) is an expected outcome the UI should display, not an
    // exceptional one.
    public async Task<(bool Success, ReceiptDto? Receipt, string? Error)> UploadReceiptAsync(
        Guid expenseId, IFormFile file, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(expenseId.ToString()), "expenseId");

        await using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(streamContent, "file", file.FileName);

        var response = await http.PostAsync("receipts", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return (false, null, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
        }

        var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>(JsonOptions, ct);
        return (true, receipt, null);
    }

    // Returns a friendly reason on 403/404 instead of throwing, since those are
    // expected outcomes the UI should display, not exceptional ones.
    public async Task<(Stream? Content, string? ContentType, string? Error)> DownloadReceiptAsync(
        Guid receiptId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"receipts/{receiptId}", ct);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return (null, null, "You're not authorized to view this receipt.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null, "Receipt not found.");
        }

        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return (await response.Content.ReadAsStreamAsync(ct), contentType, null);
    }
}
