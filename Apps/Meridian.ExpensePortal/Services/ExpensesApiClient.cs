using System.Net;
using System.Text.Json;
using Meridian.ExpensePortal.Models;

namespace Meridian.ExpensePortal.Services;

// Typed client for the Expenses API. Token attachment happens via Duende's
// AddUserAccessTokenHandler() (registered as a DelegatingHandler in Program.cs),
// so this class only knows about the Expenses API's shape — not about auth.
//
// Minimal APIs serialize with JsonSerializerDefaults.Web (camelCase)
// automatically, but HttpClient's deserializer is case-sensitive by default —
// JsonOptions is passed explicitly to every call below rather than left as
// global config.
public sealed class ExpensesApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<ExpenseDto>> GetMyExpensesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ExpenseDto>>("expenses", JsonOptions, ct) ?? [];

    public async Task<ExpenseDto?> GetExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"expenses/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExpenseDto>(JsonOptions, ct);
    }

    // Returns a friendly reason on failure instead of throwing, since a 400/403 here
    // (validation failure, wrong role, etc.) is an expected outcome the UI should
    // display, not an exceptional one.
    public async Task<(bool Success, ExpenseDto? Expense, string? Error)> CreateExpenseAsync(
        CreateExpenseRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("expenses", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return (false, null, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
        }

        var expense = await response.Content.ReadFromJsonAsync<ExpenseDto>(JsonOptions, ct);
        return (true, expense, null);
    }

    // Returns a friendly reason on failure instead of throwing, since a 403 here
    // (over the manager approval limit, wrong role, etc.) is an expected outcome
    // the UI should display, not an exceptional one.
    public async Task<(bool Success, string? Error)> UpdateExpenseStatusAsync(
        Guid id, ExpenseStatus status, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync(
            $"expenses/{id}/status", new UpdateExpenseStatusRequest(status), JsonOptions, ct);
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return (false, "You're not authorized to decide this expense.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
    }
}
