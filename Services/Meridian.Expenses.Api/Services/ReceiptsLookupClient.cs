using System.Text.Json;
using Meridian.Services.DTOs;

namespace Meridian.Expenses.Api.Services;

// Expenses.Api's first outbound call to another Meridian API — blocks Submit
// when the expense has no receipts. Authenticates as the caller via
// BearerForwardingHandler; safe here because Submit is always called by the
// expense's owner, so the receipts this returns are always the caller's own.
public sealed class ReceiptsLookupClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Fails closed by design: if Receipts.Api can't be reached, we don't know
    // whether the expense has a receipt, so Submit must not silently proceed.
    // EnsureSuccessStatusCode() is called explicitly (rather than relying on
    // GetFromJsonAsync's implicit throw) to match ExpensesLookupClient's
    // explicit-check style on the other side of this same call.
    public async Task<bool> HasAnyReceiptsAsync(Guid expenseId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"receipts?expenseId={expenseId}", ct);
        response.EnsureSuccessStatusCode();

        var receipts = await response.Content.ReadFromJsonAsync<List<ReceiptDto>>(JsonOptions, ct);
        return receipts is { Count: > 0 };
    }
}
