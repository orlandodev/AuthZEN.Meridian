using System.Net;
using System.Text.Json;
using Meridian.ExpensePortal.Models;

namespace Meridian.ExpensePortal.Services;

// Typed client for the Reporting API. Token attachment happens via the
// access-token handler registered against this client in Program.cs.
public sealed class ReportingApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<DepartmentSpendSummary>> GetDepartmentSpendAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DepartmentSpendSummary>>("reports/department-spend", JsonOptions, ct) ?? [];

    // The export endpoint returns CSV, not JSON, and is the one gated by the
    // business-hours check — so a 403 here is an expected, displayable
    // outcome, not an exceptional one. Same convention as
    // ExpensesApiClient.ApproveExpenseAsync.
    public async Task<(byte[]? Content, string? Error)> ExportDepartmentSpendAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("reports/department-spend/export", ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadAsByteArrayAsync(ct), null);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return (null, "Exports are only available during business hours.");
        }

        return (null, await ApiErrorReader.ReadErrorMessageAsync(response, ct));
    }
}
