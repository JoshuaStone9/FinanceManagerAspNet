using Microsoft.AspNetCore.Mvc;

namespace MoveInPlanner.Controllers;

public abstract class MoveInPlannerController : Controller
{
    protected bool CanEdit() => User.Identity?.IsAuthenticated == true;

    protected IActionResult LoginRedirect()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { area = "", returnUrl });
    }
}
