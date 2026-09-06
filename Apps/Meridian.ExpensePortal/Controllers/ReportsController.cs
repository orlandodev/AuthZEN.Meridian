using Meridian.ExpensePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

// [Authorize(Roles = ...)] here is cosmetic — it just avoids showing an
// employee a page that would only ever come back 403/empty. Reporting.Api
// enforces the real check: finance sees every department, a manager sees
// only their own, an employee is denied outright.
[Authorize(Roles = "manager,finance")]
public class ReportsController(ReportingApiClient reportingApi) : Controller
{
    public async Task<IActionResult> Index()
    {
        var summaries = await reportingApi.GetDepartmentSpendAsync();
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
}
