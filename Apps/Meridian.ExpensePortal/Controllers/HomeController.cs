using Microsoft.AspNetCore.Mvc;

namespace Meridian.ExpensePortal.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
