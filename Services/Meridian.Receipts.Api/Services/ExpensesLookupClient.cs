using System.Net;
using System.Text.Json;
using Meridian.Services.DTOs;

namespace Meridian.Receipts.Api.Services;

// Story 4.0: Receipts.Api's first outbound call to another Meridian API — it has no
// view of an expense's owner or status otherwise. Authenticates as the caller via
// BearerForwardingHandler, so this only ever sees what the caller themselves is
// allowed to see: safe here because upload eligibility is being checked for the
// caller's own claimed expense, never on anyone else's behalf.
public sealed class ExpensesLookupClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Null covers both "no such expense" and "caller isn't allowed to see it" —
    // callers treat both the same way: upload is denied. Only NotFound/Forbidden
    // collapse to null; any other failure (a token that expired mid-flight, or
    // Expenses.Api being briefly unavailable) throws instead of being silently
    // reported to the caller as "you're not allowed to upload" — same split
    // ExpensesApiClient.GetExpenseAsync uses on the Portal side.
    public async Task<ExpenseDto?> GetExpenseAsync(Guid expenseId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"expenses/{expenseId}", ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExpenseDto>(JsonOptions, ct);
    }
}
