using System.Net;
using Meridian.ExpensePortal.Models;
using Meridian.ExpensePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

// Mirrors Receipts.Api directly rather than bolting receipt actions onto
// ExpensesController — same split as ReportsController for the Reports API.
[Authorize]
public class ReceiptsController(ReceiptsApiClient receiptsApi, ExpensesApiClient expensesApi) : Controller
{
    public async Task<IActionResult> ForExpense(Guid expenseId)
    {
        // Independent calls to two different services — issued together rather
        // than one blocking the other in sequence; only the expense lookup's
        // result is needed before rendering can proceed, so only it is awaited
        // up front.
        var expenseTask = expensesApi.GetExpenseAsync(expenseId);
        var receiptsTask = receiptsApi.GetReceiptsForExpenseAsync(expenseId);

        // Story 4.0: the view needs the expense's owner/status to decide whether to
        // show the upload form at all — Receipts.Api itself is the real enforcement
        // (see ReceiptEndpoints.MapPost in Receipts.Api), this is UX only. A manager
        // or Finance viewer legitimately reaches this page without owning the
        // expense, same as ExpensesController.Details.
        ExpenseDto expense;
        try
        {
            var found = await expenseTask;
            if (found is null)
            {
                TempData["Error"] = "Expense not found.";
                return RedirectToAction(nameof(ExpensesController.Index), "Expenses");
            }
            expense = found;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            TempData["Error"] = "You're not authorized to view this expense.";
            return RedirectToAction(nameof(ExpensesController.Index), "Expenses");
        }

        ViewBag.ExpenseId = expenseId;
        // UX only — Receipts.Api re-checks owner+Draft server-side regardless
        // (see ReceiptEndpoints.MapPost).
        var userId = User.GetUserId();
        ViewBag.CanUpload = expense.OwnerUserId == userId && expense.Status == ExpenseStatus.Draft;
        return View(await receiptsTask);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(Guid expenseId, IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Choose a file to upload.";
            return RedirectToAction(nameof(ForExpense), new { expenseId });
        }

        var (success, _, error) = await receiptsApi.UploadReceiptAsync(expenseId, file);
        if (!success)
        {
            TempData["Error"] = error;
        }

        return RedirectToAction(nameof(ForExpense), new { expenseId });
    }

    public async Task<IActionResult> Download(Guid receiptId, Guid? expenseId)
    {
        var (content, contentType, error) = await receiptsApi.DownloadReceiptAsync(receiptId);
        if (content is null)
        {
            TempData["Error"] = error;
            return expenseId is not null
                ? RedirectToAction(nameof(ForExpense), new { expenseId })
                : RedirectToAction(nameof(ExpensesController.Index), "Expenses");
        }

        return File(content, contentType!);
    }
}
