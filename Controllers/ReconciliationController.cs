using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class ReconciliationController(FinanceRepository repo) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await repo.GetAccountReconciliationAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBalance(string sourceKey, decimal actualBalance)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(sourceKey) || actualBalance < 0m)
        {
            TempData["Error"] = "Enter a valid account balance.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await repo.UpdateReconciledAccountBalanceAsync(sourceKey, actualBalance);
            TempData["Success"] = "Recorded account balance updated successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
