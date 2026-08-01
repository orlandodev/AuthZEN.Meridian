using Meridian.ExpensePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

// Mirrors Receipts.Api directly rather than bolting receipt actions onto
// ExpensesController — same split as ReportsController for the Reports API.
[Authorize]
public class ReceiptsController(ReceiptsApiClient receiptsApi) : Controller
{
    public async Task<IActionResult> ForExpense(Guid expenseId)
    {
        ViewBag.ExpenseId = expenseId;
        var receipts = await receiptsApi.GetReceiptsForExpenseAsync(expenseId);
        return View(receipts);
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
