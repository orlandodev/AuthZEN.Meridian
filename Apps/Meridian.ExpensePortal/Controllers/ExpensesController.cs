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
