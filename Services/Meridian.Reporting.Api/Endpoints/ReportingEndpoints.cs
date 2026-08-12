using System.Globalization;
using System.Security.Claims;
using System.Text;
using Meridian.Reporting.Api.Authorization;
using Meridian.Services;
using Meridian.Services.Contracts;
using Meridian.Services.DTOs;

namespace Meridian.Reporting.Api.Endpoints;

public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports").RequireAuthorization().WithTags("Reports");

        // Finance sees every department; a manager sees only their own department.
        // Role membership is enforced declaratively via the policy below; the
        // department-scoping itself happens in IReportingService.
        group.MapGet("/department-spend", async (ClaimsPrincipal user, IReportingService reporting, CancellationToken ct) =>
            Results.Ok(await reporting.GetDepartmentSpendAsync(user.ToCallerContext(), ct)))
            .RequireAuthorization([Policies.CanViewDepartmentSpend])
            .WithSummary("Get department spend summaries")
            .WithDescription("Finance sees every department's spend summary; a manager sees only their own department's.");

        // Finance-only export, additionally gated by a business-hours check — the
        // traditional-code version of what becomes a PDP context check in Stage 4.
        group.MapGet("/department-spend/export", async (ClaimsPrincipal user, IReportingService reporting,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (!BusinessHoursPolicy.IsWithinBusinessHours(timeProvider))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Exports are only available Monday-Friday, 9am-5pm UTC.");
            }

            var summaries = await reporting.GetDepartmentSpendAsync(user.ToCallerContext(), ct);
            var csv = BuildCsv(summaries);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "department-spend.csv");
        })
            .RequireAuthorization(Policies.CanExportDepartmentSpend)
            .WithSummary("Export department spend as CSV")
            .WithDescription("Finance-only export of department spend summaries, restricted to " +
                "Monday-Friday, 9am-5pm UTC.");
    }

    private static string BuildCsv(IReadOnlyList<DepartmentSpendSummaryDto> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Department,Period,TotalAmount,Currency");
        foreach (var s in summaries)
        {
            sb.AppendLine(string.Join(',',
                CsvField(s.Department), CsvField(s.Period),
                CsvField(s.TotalAmount.ToString(CultureInfo.InvariantCulture)), CsvField(s.Currency)));
        }
        return sb.ToString();
    }

    private static string CsvField(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
