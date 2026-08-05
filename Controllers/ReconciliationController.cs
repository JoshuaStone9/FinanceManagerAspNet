using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class ReconciliationController(FinanceRepository repo) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await repo.GetAccountReconciliationAsync());


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEmergencyFundContribution(decimal amount, string? note)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (amount <= 0m)
        {
            TempData["Error"] = "Enter an emergency-fund contribution greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await repo.AddEmergencyFundContributionAsync(amount, note);
            TempData["Success"] = result.RemainingShortfall > 0m
                ? $"{amount:C} added to the Emergency Fund. Balance: {result.NewBalance:C}. Remaining to base level: {result.RemainingShortfall:C}."
                : $"{amount:C} added to the Emergency Fund. The {result.Baseline:C} base level has been reached.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReverseEmergencyFundContribution(long transactionId, string? reason)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        try
        {
            var result = await repo.ReverseEmergencyFundTransactionAsync(transactionId, reason);
            TempData["Success"] = result.RemainingShortfall > 0m
                ? $"Contribution reversed. Emergency Fund balance: {result.NewBalance:C}. Remaining to base level: {result.RemainingShortfall:C}."
                : $"Contribution reversed. Emergency Fund balance: {result.NewBalance:C}.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

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
        string? interestHandling,
        string? purpose,
        string? usageType,
        string? lastFourDigits,
        string? statementParser,
        bool isActive = true,
        bool isDefaultEmergencyFundDestination = false,
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

        var safeName = name.Trim();
        var statementOnly = string.Equals(usageType, "StatementOnly", StringComparison.OrdinalIgnoreCase);
        if (statementOnly)
        {
            startingBalance = account.StartingBalance;
            interestRate = account.InterestRate;
            monthlyContribution = account.MonthlyContribution;
            includeInGlobalGoal = false;
            taxTreatment = account.TaxTreatment;
            taxRate = account.TaxRate;
            taxEffectiveFrom = account.TaxEffectiveFrom;
            interestHandling = account.InterestHandling;
        }
        var adjustedBalance = statementOnly
            ? account.RecordedBalance
            : account.RecordedBalance + (startingBalance - account.StartingBalance);
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
            taxEffectiveFrom,
            interestHandling ?? "Keep invested",
            purpose ?? "General",
            isDefaultEmergencyFundDestination,
            usageType ?? "Tracking",
            lastFourDigits,
            statementParser ?? "Generic",
            isActive);

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
        string? interestHandling,
        string? purpose,
        string? usageType,
        string? lastFourDigits,
        string? statementParser,
        bool isActive = true,
        bool isDefaultEmergencyFundDestination = false,
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
            taxEffectiveFrom,
            interestHandling ?? "Keep invested",
            purpose ?? "General",
            isDefaultEmergencyFundDestination,
            usageType ?? "Tracking",
            lastFourDigits,
            statementParser ?? "Generic",
            isActive);

        TempData["Success"] = $"{name.Trim()} added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (id <= 0)
        {
            TempData["Error"] = "The selected account could not be found.";
            return RedirectToAction(nameof(Index));
        }

        await repo.DeleteAccountAsync(id);
        TempData["Success"] = "Account deleted.";
        return RedirectToAction(nameof(Index));
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBalance(string sourceKey, decimal actualBalance, bool reconcilePendingInterest = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(sourceKey) || actualBalance < 0m)
        {
            TempData["Error"] = "Enter a valid current account balance.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await repo.UpdateReconciledAccountBalanceAsync(sourceKey, actualBalance, reconcilePendingInterest);
            var differenceText = result.Difference == 0m
                ? "No unexplained difference remains."
                : $"There is {(result.Difference > 0m ? "more" : "less")} than logged by {Math.Abs(result.Difference):C}.";
            TempData[result.Status is "More than logged" or "Less than logged" ? "Warning" : "Success"] =
                $"{result.AccountName} saved — {result.Status}. {differenceText}" +
                (result.ReconciledInterest > 0m ? $" {result.ReconciledInterest:C} pending interest was marked as reflected." : string.Empty);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
