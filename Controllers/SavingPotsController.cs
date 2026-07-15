using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class SavingPotsController(
    FinanceRepository repo,
    IReserveRecommendationService recommendationService,
    IRecommendationApplicationService applicationService,
    IReserveAccountSelectionService reserveAccountSelectionService,
    IReservePotActionService reservePotActionService) : Controller
{
    public async Task<IActionResult> Index(bool reselect = false)
    {
        var reserve = await repo.GetHouseholdReserveAsync();
        var accountSummary = await reserveAccountSelectionService.BuildSummaryAsync();
        var pots = await repo.GetReservePotsAsync();
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        await repo.SyncFundingRemindersAsync(pots, summaries);
        var totalAllocated = pots.Where(x => x.IsActive).Sum(x => Math.Max(0m, x.AllocatedAmount));
        var remainingToAllocate = Math.Max(0m, accountSummary.SurplusAboveBaseline - totalAllocated);
        var recommendations = recommendationService.BuildRecoveryRecommendations(
            remainingToAllocate, pots, summaries);
        var dueReminders = await repo.GetFinanceRemindersAsync("Open", dueOnly: true);
        return View(new HouseholdReserveViewModel
        {
            Reserve = reserve,
            AccountSummary = accountSummary,
            ShowAccountSelector = reselect || !accountSummary.HasSelection,
            Pots = pots,
            FundingSummaries = summaries,
            RecoveryRecommendations = recommendations,
            DueReminders = dueReminders
        });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReserveAccountSelection(int[] selectedAccountIds)
    {
        if (!CanEdit()) return LoginRedirect();
        if (selectedAccountIds.Length == 0)
        {
            TempData["Error"] = "Select at least one reserve account.";
            return Redirect($"{Url.Action(nameof(Index), new { reselect = true })}#reserve-accounts");
        }

        await reserveAccountSelectionService.SaveSelectionAsync(selectedAccountIds);
        await repo.AddFinanceEventAsync("Household Reserve", "ReserveAccountsSelected", "HouseholdReserve", 1,
            "Reserve accounts updated", $"{selectedAccountIds.Length} account(s) now make up the household reserve.", null, "User");
        TempData["Success"] = "Reserve account selection saved.";
        return Redirect($"{Url.Action(nameof(Index))}#reserve-accounts");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyRecommendation(int potId, decimal amount, string operationKey)
    {
        if (!CanEdit()) return LoginRedirect();

        var result = await applicationService.ApplyAsync(potId, amount, operationKey);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return Redirect($"{Url.Action(nameof(Index))}#recommendations");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyAllRecommendations(string operationKey)
    {
        if (!CanEdit()) return LoginRedirect();

        var result = await applicationService.ApplyAllAsync(operationKey);
        TempData[result.AppliedCount > 0 ? "Success" : "Error"] = result.Message;
        return Redirect($"{Url.Action(nameof(Index))}#recommendations");
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayPotInFull(int potId, string operationKey)
    {
        if (!CanEdit()) return LoginRedirect();
        var result = await reservePotActionService.PayInFullAsync(potId, operationKey);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return Redirect($"{Url.Action(nameof(Index))}#pot-{potId}");
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        var model = await reservePotActionService.BuildWithdrawalAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(ReservePotWithdrawalViewModel input)
    {
        if (!CanEdit()) return LoginRedirect();
        var result = await reservePotActionService.WithdrawAsync(input);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Message;
            var refreshed = await reservePotActionService.BuildWithdrawalAsync(input.PotId);
            if (refreshed is null) return NotFound();
            refreshed.Amount = input.Amount;
            refreshed.WithdrawalDate = input.WithdrawalDate;
            refreshed.Reason = input.Reason;
            refreshed.OperationKey = input.OperationKey;
            return View(refreshed);
        }

        TempData["Success"] = result.Message;
        return Redirect($"{Url.Action(nameof(Index))}#pot-{input.PotId}");
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
    public async Task<IActionResult> SavePot(int id, string name, decimal allocatedAmount, decimal defaultMonthlyContribution, decimal intendedMonthlyContribution, string fundingFrequency, int? expectedFundingDay, bool carryForwardShortfalls, bool carryExcessForward, DateTime? fundingPausedFrom, DateTime? fundingPausedUntil, string? fundingPauseReason, DateTime? fundingPlanStartDate, decimal? targetAmount, DateTime? dueDate, int priority, bool isActive, string? notes)
    {
        if (!CanEdit()) return LoginRedirect();

        var returnAnchor = id > 0 ? $"pot-{id}" : "new-allocation";
        try
        {
            var savedId = await repo.SaveReservePotAsync(id, name, allocatedAmount, defaultMonthlyContribution, intendedMonthlyContribution, fundingFrequency, expectedFundingDay, carryForwardShortfalls, carryExcessForward, fundingPausedFrom, fundingPausedUntil, fundingPauseReason, fundingPlanStartDate, targetAmount, dueDate, priority, isActive, notes);
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
