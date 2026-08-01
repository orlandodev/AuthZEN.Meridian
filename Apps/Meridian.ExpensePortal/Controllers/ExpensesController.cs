using System.Net;
using Meridian.ExpensePortal.Models;
using Meridian.ExpensePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

// Cosmetic role checks live in the views (User.IsInRole in Razor). This
// controller enforces nothing itself — the Expenses API is the enforcement
// point, and it re-checks every request independently. That split is the
// whole point of the reference implementation.
[Authorize]
public class ExpensesController(ExpensesApiClient expensesApi) : Controller
{
    public async Task<IActionResult> Index()
    {
        var expenses = await expensesApi.GetMyExpensesAsync();
        return View(expenses);
    }

    // Unlike Index (always scoped to the caller's own expenses), this reads a
    // single expense by id — the same resource-based check Approve/Reject relies
    // on (owner, same-department manager, or finance). GetExpenseAsync throws on
    // a non-owner/non-privileged caller's 403, since nothing else in the Portal
    // called this method before now.
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var expense = await expensesApi.GetExpenseAsync(id);
            return expense is null ? NotFound() : View(expense);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            TempData["Error"] = "You're not authorized to view this expense.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExpenseRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid amount and category.";
            return RedirectToAction(nameof(Index));
        }

        var (success, _, error) = await expensesApi.CreateExpenseAsync(request);
        if (!success)
        {
            TempData["Error"] = error;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var (success, error) = await expensesApi.UpdateExpenseStatusAsync(id, ExpenseStatus.Approved);

        if (!success)
        {
            TempData["Error"] = error;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id)
    {
        var (success, error) = await expensesApi.UpdateExpenseStatusAsync(id, ExpenseStatus.Rejected);

        if (!success)
        {
            TempData["Error"] = error;
        }

        return RedirectToAction(nameof(Index));
    }
}
