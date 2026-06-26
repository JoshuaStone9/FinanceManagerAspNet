using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class SavingPotsController(FinanceRepository repo) : Controller
{
    public async Task<IActionResult> Index(int? year)
    {
        var selectedYear = year ?? DateTime.Today.Year;
        var savedEmergencyFund = await repo.GetEmergencyFundAsync();
        var emergencyFund = await repo.GetDecimalSettingAsync("LastCalculatedEmergencyFund", savedEmergencyFund);
        var pots = await repo.GetSavingPotsAsync();
        var months = await repo.GetSavingPotMonthsAsync(selectedYear);
        var allocated = await repo.GetTotalAllocatedToSavingPotsAsync();

        var vm = new SavingPotsViewModel
        {
            Year = selectedYear,
            EmergencyFundTotal = emergencyFund,
            AllocatedToPots = allocated,
            Pots = pots.Select(p => new SavingPotRowViewModel
            {
                Pot = p,
                Months = months.Where(m => m.SavingPotId == p.Id).ToList()
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePot(int id, string name, decimal targetAmount, decimal monthlyAmount, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveSavingPotAsync(id, name, Math.Max(0, targetAmount), Math.Max(0, monthlyAmount));
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePot(int id, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeleteSavingPotAsync(id);
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMonth(int potId, int year, int month)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.ToggleSavingPotMonthAsync(potId, year, month);
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExtra(int potId, decimal amount, DateTime date, string? note, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.AddSavingPotExtraAsync(potId, Math.Max(0, amount), date, note);
        return RedirectToAction(nameof(Index), new { year });
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;

    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });

}
