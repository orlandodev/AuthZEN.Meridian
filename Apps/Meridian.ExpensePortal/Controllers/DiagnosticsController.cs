using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

// Kept as a permanent tool, not scaffolding to delete later — genuinely useful
// whenever role- or ownership-based behavior doesn't match expectations.
[Authorize]
public class DiagnosticsController : Controller
{
    public IActionResult Claims() => View();
}
