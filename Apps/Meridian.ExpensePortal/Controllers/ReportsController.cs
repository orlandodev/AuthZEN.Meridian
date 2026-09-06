using Meridian.ExpensePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

// [Authorize(Roles = ...)] here is cosmetic — it just avoids showing an
// employee a page that would only ever come back 403/empty. Reporting.Api
// enforces the real check: finance sees every department, a manager sees
// only their own, an employee is denied outright.
[Authorize(Roles = "manager,finance")]
public class ReportsController(ReportingApiClient reportingApi, IConfiguration configuration) : Controller
{
    public async Task<IActionResult> Index()
    {
        var summaries = await reportingApi.GetDepartmentSpendAsync();
        ViewData["ExportWindow"] = BuildExportWindowText();
        return View(summaries);
    }

    // Deliberately not a JSON/AJAX action — a real file download, so the
    // business-hours 403 (if it happens) surfaces as a redirect back to
    // Index with a visible error rather than a silent failed fetch.
    public async Task<IActionResult> Export()
    {
        var (content, error) = await reportingApi.ExportDepartmentSpendAsync();
        if (content is null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }
        return File(content, "text/csv", "department-spend.csv");
    }

    // The window itself is enforced by the PDP (DepartmentSpendRules.CanExport);
    // this only names the same timezone the PDP is configured with — see
    // BusinessHours:TimeZone, set by the AppHost — so the displayed hours can't
    // drift from the enforced ones. Required, same as on the PDP: a missing key
    // is a misconfiguration, not something to paper over with a default.
    private string BuildExportWindowText()
    {
        var timeZoneId = configuration["BusinessHours:TimeZone"];
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new InvalidOperationException("Missing configuration: BusinessHours:TimeZone");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var label = timeZone.StandardName.Replace(" Standard Time", " Time");
        return $"Monday–Friday, 9:00 AM–5:00 PM {label}";
    }
}
