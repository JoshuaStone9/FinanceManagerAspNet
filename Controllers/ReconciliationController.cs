using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class ReconciliationController(FinanceRepository repo) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await repo.GetAccountReconciliationAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAccount(
        int id,
        string name,
        decimal interestRate,
        decimal monthlyContribution,
        decimal startingBalance,
        string? provider,
        string? accountType,
        string? holdingType,
        string? taxTreatment,
        decimal taxRate,
        DateTime? taxEffectiveFrom,
        bool includeInGlobalGoal = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(name) || interestRate < 0m || monthlyContribution < 0m || startingBalance < 0m)
        {
            TempData["Error"] = "Enter valid account details.";
            return RedirectToAction(nameof(Index));
        }

        var accounts = await repo.GetAccountReconciliationAsync();
        var account = accounts.Accounts.FirstOrDefault(x => x.AccountId == id);
        if (account is null)
        {
            TempData["Error"] = "The selected account could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var safeName = id == 0 ? "Emergency Fund" : name.Trim();
        var adjustedBalance = account.RecordedBalance + (startingBalance - account.StartingBalance);
        await repo.SaveAccountAsync(
            id,
            safeName,
            adjustedBalance,
            interestRate,
            monthlyContribution,
            includeInGlobalGoal,
            startingBalance,
            provider ?? "Other",
            accountType ?? "Savings",
            holdingType ?? "Cash",
            taxTreatment ?? "Tax Free",
            taxRate,
            taxEffectiveFrom);

        TempData["Success"] = $"{safeName} settings updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccount(
        string name,
        decimal openingBalance,
        decimal interestRate,
        decimal monthlyContribution,
        string? provider,
        string? accountType,
        string? holdingType,
        string? taxTreatment,
        decimal taxRate,
        DateTime? taxEffectiveFrom,
        bool includeInGlobalGoal = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(name) || openingBalance < 0m || interestRate < 0m || monthlyContribution < 0m)
        {
            TempData["Error"] = "Enter valid details for the new account.";
            return RedirectToAction(nameof(Index));
        }

        await repo.SaveAccountAsync(
            0,
            name.Trim(),
            openingBalance,
            interestRate,
            monthlyContribution,
            includeInGlobalGoal,
            openingBalance,
            provider ?? "Other",
            accountType ?? "Savings",
            holdingType ?? "Cash",
            taxTreatment ?? "Tax Free",
            taxRate,
            taxEffectiveFrom);

        TempData["Success"] = $"{name.Trim()} added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (id <= 0)
        {
            TempData["Error"] = "The Emergency Fund cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        await repo.DeleteAccountAsync(id);
        TempData["Success"] = "Account deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBalance(string sourceKey, decimal actualBalance)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(sourceKey) || actualBalance < 0m)
        {
            TempData["Error"] = "Enter a valid current account balance.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await repo.UpdateReconciledAccountBalanceAsync(sourceKey, actualBalance);
            TempData["Success"] = "Current account balance saved. No income entry was created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
