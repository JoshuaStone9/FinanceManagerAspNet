using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class SavingPotsController(FinanceRepository repo) : Controller
{
    public async Task<IActionResult> Index()
    {
        var reserve = await repo.GetHouseholdReserveAsync();
        var pots = await repo.GetReservePotsAsync();
        return View(new HouseholdReserveViewModel { Reserve = reserve, Pots = pots });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReserve(decimal balance, decimal interestRate, string? provider)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveHouseholdReserveAsync(balance, interestRate, provider);
        TempData["Success"] = "Household reserve updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePot(int id, string name, decimal allocatedAmount, decimal defaultMonthlyContribution, decimal? targetAmount, DateTime? dueDate, int priority, bool isActive, string? notes)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveReservePotAsync(id, name, allocatedAmount, defaultMonthlyContribution, targetAmount, dueDate, priority, isActive, notes);
        TempData["Success"] = id > 0 ? "Virtual pot updated." : "Virtual pot added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePot(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeleteReservePotAsync(id);
        TempData["Success"] = "Virtual pot deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
