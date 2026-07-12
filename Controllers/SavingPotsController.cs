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
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        return View(new HouseholdReserveViewModel { Reserve = reserve, Pots = pots, FundingSummaries = summaries });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReserve(decimal balance, decimal interestRate, string? provider)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveHouseholdReserveAsync(balance, interestRate, provider);
        TempData["Success"] = "Household reserve updated.";
        return Redirect($"{Url.Action(nameof(Index))}#reserve-account");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePot(int id, string name, decimal allocatedAmount, decimal defaultMonthlyContribution, decimal intendedMonthlyContribution, string fundingFrequency, int? expectedFundingDay, bool carryForwardShortfalls, bool carryExcessForward, DateTime? fundingPausedUntil, string? fundingPauseReason, DateTime? fundingPlanStartDate, decimal? targetAmount, DateTime? dueDate, int priority, bool isActive, string? notes)
    {
        if (!CanEdit()) return LoginRedirect();

        var returnAnchor = id > 0 ? $"pot-{id}" : "new-allocation";
        try
        {
            var savedId = await repo.SaveReservePotAsync(id, name, allocatedAmount, defaultMonthlyContribution, intendedMonthlyContribution, fundingFrequency, expectedFundingDay, carryForwardShortfalls, carryExcessForward, fundingPausedUntil, fundingPauseReason, fundingPlanStartDate, targetAmount, dueDate, priority, isActive, notes);
            TempData["Success"] = id > 0 ? "Virtual pot updated." : "Virtual pot added.";
            return Redirect($"{Url.Action(nameof(Index))}#pot-{savedId}");
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
            return Redirect($"{Url.Action(nameof(Index))}#{returnAnchor}");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildHistory(int id, string startMode, DateTime? customStartDate)
    {
        if (!CanEdit()) return LoginRedirect();
        var pots = await repo.GetReservePotsAsync();
        var pot = pots.FirstOrDefault(x => x.Id == id);
        if (pot is null) return RedirectToAction(nameof(Index));

        DateTime startDate;
        if (startMode == "FirstContribution")
        {
            startDate = await repo.GetFirstReservePotContributionDateAsync(pot.Name) ?? new DateTime(2026, 1, 1);
        }
        else if (startMode == "January2026") startDate = new DateTime(2026, 1, 1);
        else startDate = customStartDate ?? pot.FundingPlanStartDate;

        await repo.RebuildReservePotFundingHistoryAsync(id, startDate);
        await repo.AddFinanceEventAsync("Household Reserve", "FundingHistoryRebuilt", "ReservePot", id, $"{pot.Name} funding history rebuilt", $"Funding history now starts {startDate:dd MMM yyyy}.", null, "User");
        TempData["Success"] = "Funding history recalculated.";
        return Redirect($"{Url.Action(nameof(Index))}#pot-{id}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePot(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeleteReservePotAsync(id);
        TempData["Success"] = "Virtual pot deleted.";
        return Redirect($"{Url.Action(nameof(Index))}#allocations");
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
