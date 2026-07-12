using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using FinanceManagerAspNet.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class StatisticsController(FinanceRepository repo, FinanceCalculator calc, IConfiguration config, AppDbContext vaultDb) : Controller
{
    public async Task<IActionResult> Index(
        decimal? externalTotalValue,
        decimal? houseGoal,
        decimal? moneyboxFundAmount,
        decimal? moneyboxInterestRate,
        decimal? moneyboxBonus,
        int? forecastMonths,
        decimal? standaloneInterestAmount,
        decimal? standaloneInterestRate,
        decimal? standaloneMonthlyContribution,
        int? standaloneMonths,
        List<int>? selectedAccountIds,
        DateTime? goalDate,
        decimal? deductFromTotal)
    {
        await repo.EnsureModernTablesAsync();

        var emergency = await repo.GetEmergencyFundAsync();
        var accounts = await repo.GetAccountsAsync(emergency);
        var income = await repo.GetIncomeHistoryAsync();

        var goal = decimal.TryParse(config["FinanceSettings:GlobalGoal"], out var gg) ? gg : 20000m;
        var monthlyTarget = decimal.TryParse(config["FinanceSettings:MonthlySavingTarget"], out var mt) ? mt : 1200m;
        var defaultTarget = new DateTime(DateTime.Today.Year + (DateTime.Today.Month > 4 ? 1 : 0), 4, 30);
        var target = (goalDate ?? defaultTarget).Date;
        var months = Math.Max(0, ((target.Year - DateTime.Today.Year) * 12) + target.Month - DateTime.Today.Month);

        selectedAccountIds ??= [];
        var validIds = accounts.Select(a => a.Id).ToHashSet();
        var selectedIds = selectedAccountIds.Where(validIds.Contains).Distinct().ToList();
        if (selectedIds.Count == 0 && accounts.Count > 0) selectedIds.Add((accounts.FirstOrDefault(a => a.Id != 0) ?? accounts[0]).Id);
        var selectedAccounts = accounts.Where(a => selectedIds.Contains(a.Id)).ToList();

        var included = accounts.Where(a => a.IncludeInGlobalGoal).ToList();
        var total = included.Sum(a => a.Amount);
        var averageIncome = income.Count == 0 ? 3500m : income.Average(x => x.Amount);
        var monthlyContrib = included.Sum(x => x.MonthlyContribution);
        var projections = calc.ProjectAccountsDetailed(included, months);
        var projectedSalarySavings = calc.ProjectSalarySavings(monthlyTarget, months);
        var projectedWithoutInterest = calc.ProjectAccountsWithoutInterest(included, months, monthlyTarget);
        var projectedWithInterest = calc.ProjectAccounts(included, months, monthlyTarget);
        var monthsToGoalWithInterest = calc.MonthsToGoalWithInterest(included, goal, monthlyTarget);
        var stocksCrypto = await repo.GetStocksCryptoAsync();
        var assetSummary = await repo.GetAssetSummaryAsync();
        var projectedStocksCrypto = calc.CompoundMonthly(assetSummary.TotalValue, assetSummary.WeightedGrowthRate, assetSummary.MonthlyContribution, months);
        var vaultInvestedCategoryNames = new[] { "Coins & Bullion", "Coins", "Bullion", "Gold", "Silver" };
        var vaultItemsForFinance = vaultDb.Items
            .Where(i => i.Category == null || !vaultInvestedCategoryNames.Contains(i.Category.Name));
        var vaultItemCount = await vaultItemsForFinance.CountAsync();
        var vaultTotalValue = await vaultItemsForFinance.SumAsync(i => i.CurrentValue ?? 0);

        var allocatedToSavingPots = await repo.GetTotalAllocatedToSavingPotsAsync();

        // Default "total value of everything" to the April forecast with interest unless a value is manually entered.
        externalTotalValue ??= Math.Round(projectedWithInterest + projectedStocksCrypto + vaultTotalValue, 2);

        var house = BuildHouseGoalModel(
            calc,
            externalTotalValue,
            houseGoal,
            moneyboxFundAmount,
            moneyboxInterestRate,
            moneyboxBonus,
            forecastMonths ?? months,
            monthlyTarget,
            standaloneInterestAmount,
            standaloneInterestRate,
            standaloneMonthlyContribution,
            standaloneMonths ?? months);

        if (externalTotalValue.HasValue && User.Identity?.IsAuthenticated == true)
        {
            await repo.SaveDecimalSettingAsync("LastCalculatedEmergencyFund", house.EmergencyFundStillNeededWithInterest);
        }

        var recentUpdates = accounts
            .Where(a => a.UpdatedAt > DateTime.MinValue)
            .OrderByDescending(a => a.UpdatedAt)
            .ToList();

        var now = DateTime.Today;
        var fallbackIncome = decimal.TryParse(config["FinanceSettings:DefaultMonthlyIncome"], out var di) ? di : 3500m;
        var currentIncome = await repo.GetIncomeAsync(now.Year, now.Month);
        var currentAllowance = currentIncome?.Amount ?? await repo.GetMonthlyAllowanceAsync(now.Month, fallbackIncome);
        var currentBills = await repo.GetRowsAsync("bills", now.Month, now.Year);
        var currentExpenses = await repo.GetRowsAsync("extra_expenses", now.Month, now.Year);
        var currentInvestments = await repo.GetRowsAsync("investments", now.Month, now.Year);
        var currentSavings = await repo.GetRowsAsync("savings", now.Month, now.Year);
        var currentRemaining = currentAllowance - currentBills.Sum(x => x.Amount) - currentExpenses.Sum(x => x.Amount) - currentInvestments.Sum(x => x.Amount) + currentSavings.Sum(x => x.Amount);
        var currentMonthVariance = currentRemaining - monthlyTarget;
        var selectedDeduction = Math.Max(0, deductFromTotal ?? 0m);
        var selectedRawTotal = selectedAccounts.Sum(a => a.Amount);
        var selectedTotalNow = Math.Max(0, selectedRawTotal - selectedDeduction);

        // The headline forecast always uses the single configured monthly saving target (£1,200 by default),
        // rather than adding every account's "Monthly in" amount on top of it.
        var selectedWithoutInterest = selectedTotalNow + calc.ProjectSalarySavings(monthlyTarget, months);

        // Spread the £1,200 target across selected pots using their recorded monthly-in values as weights.
        // This preserves the existing monthly-in settings without double-counting them in the headline total.
        decimal selectedWithInterest;
        if (selectedAccounts.Count == 0)
        {
            selectedWithInterest = 0m;
        }
        else
        {
            var rawMonthlyTotal = selectedAccounts.Sum(a => Math.Max(0, a.MonthlyContribution));
            selectedWithInterest = 0m;
            var remainingDeduction = selectedDeduction;

            for (var index = 0; index < selectedAccounts.Count; index++)
            {
                var account = selectedAccounts[index];
                var accountOpeningBalance = Math.Max(0, account.Amount - remainingDeduction);
                remainingDeduction = Math.Max(0, remainingDeduction - account.Amount);

                var allocatedMonthlyTarget = rawMonthlyTotal > 0
                    ? monthlyTarget * Math.Max(0, account.MonthlyContribution) / rawMonthlyTotal
                    : (index == 0 ? monthlyTarget : 0m);

                selectedWithInterest += calc.CompoundMonthly(
                    accountOpeningBalance,
                    account.InterestRate,
                    allocatedMonthlyTarget,
                    months);
            }
        }

        var pattern = recentUpdates.Count switch
        {
            0 => "No update pattern yet. Update your ISA/pots monthly and this will become more useful.",
            1 => $"Only one balance update is recorded so far: {recentUpdates[0].Name} on {recentUpdates[0].UpdatedAt:dd MMM yyyy}.",
            _ => $"Most recent update: {recentUpdates[0].Name} on {recentUpdates[0].UpdatedAt:dd MMM yyyy}. You have {recentUpdates.Count} tracked account balances with update dates."
        };

        var vm = new StatisticsViewModel
        {
            GlobalGoal = goal,
            TotalNow = total,
            Remaining = Math.Max(0, goal - total),
            Accounts = accounts,
            IncomeHistory = income,
            ManualAverageIncome = Math.Round(averageIncome, 2),
            CalculatedSalaryEstimate = Math.Round((26000m + 26250m) / 12m * 0.805m, 2),
            AverageSavingPace = monthlyContrib,
            MonthlySavingTarget = monthlyTarget,
            AprilTarget = target,
            MonthsToApril = months,
            ProjectedSalarySavingsByApril = projectedSalarySavings,
            ProjectedByAprilWithoutInterest = Math.Round(projectedWithoutInterest, 2),
            ProjectedByAprilWithInterest = Math.Round(projectedWithInterest, 2),
            ProjectedInterestEarned = Math.Round(projectedWithInterest - projectedWithoutInterest, 2),
            EstimatedMonthsToGoal = monthsToGoalWithInterest,
            EstimatedGoalDate = monthsToGoalWithInterest < 0 ? null : DateTime.Today.AddMonths(monthsToGoalWithInterest),
            InterestProjections = projections,
            UpdatePattern = accounts.Select(a => new LastModifiedInfo(a.Name, a.UpdatedAt == DateTime.MinValue ? null : a.UpdatedAt)).ToList(),
            PatternMessage = pattern,
            HouseGoal = house,
            AllocatedToSavingPots = allocatedToSavingPots,
            StocksCryptoValue = stocksCrypto.Amount,
            LiveAssetsValue = assetSummary.TotalValue,
            LiveAssetsMonthlyContribution = assetSummary.MonthlyContribution,
            LiveAssetsGrowthRate = assetSummary.WeightedGrowthRate,
            LiveAssetsLastUpdated = assetSummary.LastUpdated,
            StocksCryptoInterestRate = stocksCrypto.Rate,
            StocksCryptoMonthlyContribution = stocksCrypto.Monthly,
            ProjectedStocksCryptoByApril = Math.Round(projectedStocksCrypto, 2),
            VaultTotalValue = vaultTotalValue,
            VaultItemCount = vaultItemCount,
            TotalValueNow = Math.Round(total + assetSummary.TotalValue + vaultTotalValue, 2),
            TotalValueByApril = Math.Round(projectedWithInterest + projectedStocksCrypto + vaultTotalValue, 2),
            SelectedAccountIds = selectedIds,
            GoalDate = target,
            SelectedTotalNow = Math.Round(selectedTotalNow, 2),
            SelectedProjectedWithoutInterest = Math.Round(selectedWithoutInterest, 2),
            SelectedProjectedWithInterest = Math.Round(selectedWithInterest, 2),
            DeductFromSelectedTotal = Math.Round(selectedDeduction, 2),
            CurrentMonthVariance = Math.Round(currentMonthVariance, 2)
        };

        return View(vm);
    }


    [HttpPost]
    public async Task<IActionResult> SaveAccount(int id, string name, decimal amount, decimal interestRate, decimal monthlyContribution, bool includeInGlobalGoal = true)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
        await repo.SaveAccountAsync(id, name, amount, interestRate, monthlyContribution, includeInGlobalGoal);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
        await repo.DeleteAccountAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SaveStocksCrypto(decimal amount, decimal interestRate, decimal monthlyContribution)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
        await repo.SaveStocksCryptoAsync(amount, interestRate, monthlyContribution);
        return RedirectToAction(nameof(Index));
    }

    private static HouseGoalViewModel BuildHouseGoalModel(
        FinanceCalculator calc,
        decimal? externalTotalValue,
        decimal? houseGoal,
        decimal? moneyboxFundAmount,
        decimal? moneyboxInterestRate,
        decimal? moneyboxBonus,
        int forecastMonths,
        decimal monthlySavingTarget,
        decimal? standaloneInterestAmount,
        decimal? standaloneInterestRate,
        decimal? standaloneMonthlyContribution,
        int standaloneMonths)
    {
        var safeMonths = Math.Max(0, forecastMonths);
        var safeStandaloneMonths = Math.Max(0, standaloneMonths);
        var external = Math.Max(0, externalTotalValue ?? 0m);
        var goal = Math.Max(0, houseGoal ?? 30000m);
        var moneybox = Math.Max(0, moneyboxFundAmount ?? 0m);
        var moneyboxRate = Math.Max(0, moneyboxInterestRate ?? 3.8m);
        var bonus = Math.Max(0, moneyboxBonus ?? 0m);

        // Simplified emergency fund calculation:
        // Emergency Fund = Total Value - £30,000 House Target - Moneybox Interest Made - Moneybox Bonus.
        // The £1,200/month planned saving is used only to estimate Moneybox interest, not added to Emergency Fund.
        var plannedHouseSavings = calc.ProjectSalarySavings(monthlySavingTarget, safeMonths);
        var moneyboxProjected = calc.CompoundMonthly(moneybox, moneyboxRate, monthlySavingTarget, safeMonths);
        var moneyboxInterestOnly = Math.Max(0, Math.Round(moneyboxProjected - moneybox - plannedHouseSavings, 2));

        var amountRemovedWithoutInterest = goal + bonus;
        var amountRemovedWithInterest = goal + moneyboxInterestOnly + bonus;
        var emergencyFundWithoutInterest = Math.Round(external - amountRemovedWithoutInterest, 2);
        var emergencyFundWithInterest = Math.Round(external - amountRemovedWithInterest, 2);

        var standaloneAmount = Math.Max(0, standaloneInterestAmount ?? moneybox);
        var standaloneRate = Math.Max(0, standaloneInterestRate ?? moneyboxRate);
        var standaloneMonthly = Math.Max(0, standaloneMonthlyContribution ?? 0m);
        var standaloneProjected = calc.CompoundMonthly(standaloneAmount, standaloneRate, standaloneMonthly, safeStandaloneMonths);
        var standaloneInterest = Math.Max(0, Math.Round(standaloneProjected - standaloneAmount - (standaloneMonthly * safeStandaloneMonths), 2));

        return new HouseGoalViewModel
        {
            ExternalTotalValue = external,
            HouseGoal = goal,
            MoneyboxFundAmount = moneybox,
            MoneyboxInterestRate = moneyboxRate,
            MoneyboxBonus = bonus,
            ForecastMonths = safeMonths,
            MonthlyHouseSaving = monthlySavingTarget,
            PlannedHouseSavings = plannedHouseSavings,
            MoneyboxProjectedBalance = moneyboxProjected,
            MoneyboxInterestOnly = moneyboxInterestOnly,
            HouseNormalTotal = Math.Round(amountRemovedWithoutInterest, 2),
            HouseWithInterestTotal = Math.Round(amountRemovedWithInterest, 2),
            HouseRemainingNormal = emergencyFundWithoutInterest,
            HouseRemainingWithInterest = emergencyFundWithInterest,
            EmergencyFundStillNeededNormal = emergencyFundWithoutInterest,
            EmergencyFundStillNeededWithInterest = emergencyFundWithInterest,
            StandaloneInterestAmount = standaloneAmount,
            StandaloneInterestRate = standaloneRate,
            StandaloneMonthlyContribution = standaloneMonthly,
            StandaloneMonths = safeStandaloneMonths,
            StandaloneInterestMade = standaloneInterest,
            StandaloneProjectedTotal = standaloneProjected
        };
    }
}
