using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using FinanceManagerAspNet.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class StatisticsController(
    FinanceRepository repo,
    FinanceCalculator calc,
    IConfiguration config,
    AppDbContext vaultDb,
    IReserveInterestForecastService interestForecastService) : Controller
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
        int? standaloneMonths)
    {
        await repo.EnsureModernTablesAsync();

        var emergency = await repo.GetEmergencyFundAsync();
        var accounts = await repo.GetAccountsAsync(emergency);
        var income = await repo.GetIncomeHistoryAsync();

        var goal = decimal.TryParse(config["FinanceSettings:GlobalGoal"], out var gg) ? gg : 20000m;
        var defaultGoal = new DateTime(DateTime.Today.Year + 1, 1, 31);
        var goalYear = (int)await repo.GetDecimalSettingAsync("StatisticsGoalYear", defaultGoal.Year);
        var goalMonth = Math.Clamp((int)await repo.GetDecimalSettingAsync("StatisticsGoalMonth", defaultGoal.Month), 1, 12);
        var targetYear = Math.Max(DateTime.Today.Year, goalYear);
        var target = new DateTime(targetYear, goalMonth, DateTime.DaysInMonth(targetYear, goalMonth));
        if (target < DateTime.Today) target = defaultGoal;
        var months = Math.Max(0, ((target.Year - DateTime.Today.Year) * 12) + target.Month - DateTime.Today.Month);

        var included = accounts.Where(a => a.IncludeInGlobalGoal).ToList();
        var total = included.Sum(a => a.Amount);
        var averageIncome = income.Count == 0 ? 3500m : income.Average(x => x.Amount);
        var accountMonthlyContributions = included.Sum(x => x.MonthlyContribution);
        var forecast = await BuildForecastAssumptionAsync();

        var forecastAccounts = included.Select(x => new ReserveAccountOption(
            x.Id,
            x.Name,
            x.Amount,
            x.InterestRate,
            x.MonthlyContribution,
            true,
            x.TaxTreatment,
            x.TaxRate,
            x.TaxEffectiveFrom)).ToList();
        var taxAwareAccountForecast = interestForecastService.Build(forecastAccounts, target, includeFutureContributions: true);
        var projections = taxAwareAccountForecast.Accounts.Select(x => new InterestProjection(
            x.AccountName,
            x.OpeningBalance,
            x.AnnualInterestRate,
            x.MonthlyContribution,
            months,
            x.ContributionsAdded,
            x.NetInterest,
            x.ProjectedBalance,
            included.First(a => a.Id == x.AccountId).UpdatedAt)).ToList();
        var baseProjectionWithoutInterest = total + taxAwareAccountForecast.FutureContributions;
        var weightedRate = CalculateWeightedNetRate(included, target);
        var forecastContributionProjection = calc.CompoundMonthly(0m, weightedRate, forecast.MonthlyContribution, months);
        var forecastContributions = calc.ProjectSalarySavings(forecast.MonthlyContribution, months);
        var forecastContributionInterest = Math.Max(0m, forecastContributionProjection - forecastContributions);
        var projectedGrossInterest = Math.Round(taxAwareAccountForecast.GrossInterest + forecastContributionInterest, 2);
        var projectedInterestTax = Math.Round(taxAwareAccountForecast.EstimatedTax, 2);
        var projectedFutureInterest = Math.Round(taxAwareAccountForecast.EstimatedInterest + forecastContributionInterest, 2);
        var projectedWithoutInterest = Math.Round(baseProjectionWithoutInterest + forecastContributions, 2);
        var projectedWithInterest = Math.Round(projectedWithoutInterest + projectedFutureInterest, 2);

        var currentMonthPassiveIncome = await repo.GetPassiveIncomeEstimatesAsync(DateTime.Today.Year, DateTime.Today.Month);
        var expectedInterestThisMonth = currentMonthPassiveIncome
            .Where(x => !x.IsReceived)
            .Sum(x => x.EstimatedAmount);
        var interestReceivedThisMonth = currentMonthPassiveIncome
            .Where(x => x.IsReceived)
            .Sum(x => x.ActualAmount ?? 0m);
        var interestReceivedThisYear = await repo.GetPassiveInterestTotalAsync(
            new DateTime(DateTime.Today.Year, 1, 1),
            new DateTime(DateTime.Today.Year + 1, 1, 1));
        var totalMonthlyForecastPace = accountMonthlyContributions + forecast.MonthlyContribution;
        var monthsToGoalWithInterest = CalculateMonthsToGoalTaxAware(included, goal, forecast.MonthlyContribution, interestForecastService);

        var stocksCrypto = await repo.GetStocksCryptoAsync();
        var assetSummary = await repo.GetAssetSummaryAsync();
        var projectedStocksCrypto = calc.CompoundMonthly(assetSummary.TotalValue, assetSummary.WeightedGrowthRate, assetSummary.MonthlyContribution, months);
        var vaultInvestedCategoryNames = new[] { "Coins & Bullion", "Coins", "Bullion", "Gold", "Silver" };
        var vaultItemsForFinance = vaultDb.Items
            .Where(i => i.Category == null || !vaultInvestedCategoryNames.Contains(i.Category.Name));
        var vaultItemCount = await vaultItemsForFinance.CountAsync();
        var vaultTotalValue = await vaultItemsForFinance.SumAsync(i => i.CurrentValue ?? 0);

        var allocatedToSavingPots = await repo.GetTotalAllocatedToSavingPotsAsync();
        externalTotalValue ??= Math.Round(projectedWithInterest + projectedStocksCrypto + vaultTotalValue, 2);

        var house = BuildHouseGoalModel(
            calc,
            externalTotalValue,
            houseGoal,
            moneyboxFundAmount,
            moneyboxInterestRate,
            moneyboxBonus,
            forecastMonths ?? months,
            forecast.MonthlyContribution,
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
            AverageSavingPace = totalMonthlyForecastPace,
            MonthlyForecastContribution = forecast.MonthlyContribution,
            ForecastMethod = forecast.Method,
            ForecastMethodLabel = forecast.MethodLabel,
            ForecastConfidence = forecast.Confidence,
            ForecastConfidenceCssClass = forecast.ConfidenceCssClass,
            ForecastHistoryCount = forecast.Months.Count,
            ForecastHistory = forecast.Months,
            AccountMonthlyContributions = accountMonthlyContributions,
            ForecastContributionsByGoalDate = forecastContributions,
            AccountContributionsByGoalDate = Math.Round(accountMonthlyContributions * months, 2),
            ForecastContributionInterest = Math.Round(forecastContributionInterest, 2),
            ForecastWeightedInterestRate = Math.Round(weightedRate, 2),
            InterestReceivedInForecastHistory = forecast.InterestReceived,
            InterestReceivedThisYear = Math.Round(interestReceivedThisYear, 2),
            ExpectedInterestThisMonth = Math.Round(expectedInterestThisMonth, 2),
            InterestReceivedThisMonth = Math.Round(interestReceivedThisMonth, 2),
            ProjectedFutureInterest = projectedFutureInterest,
            ProjectedGrossInterest = projectedGrossInterest,
            ProjectedInterestTax = projectedInterestTax,
            AprilTarget = target,
            MonthsToApril = months,
            ProjectedSalarySavingsByApril = forecastContributions,
            ProjectedByAprilWithoutInterest = projectedWithoutInterest,
            ProjectedByAprilWithInterest = projectedWithInterest,
            ProjectedInterestEarned = projectedFutureInterest,
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
            TotalValueByApril = Math.Round(projectedWithInterest + projectedStocksCrypto + vaultTotalValue, 2)
        };

        return View(vm);
    }

    private static decimal CalculateWeightedNetRate(IReadOnlyList<AccountBalance> accounts, DateTime forecastDate)
    {
        var total = accounts.Sum(x => Math.Max(0m, x.Amount));
        if (total <= 0m) return 0m;

        return accounts.Sum(account =>
        {
            var taxable = account.TaxTreatment.Equals("Taxable", StringComparison.OrdinalIgnoreCase)
                && (!account.TaxEffectiveFrom.HasValue || forecastDate.Date >= account.TaxEffectiveFrom.Value.Date);
            var netRate = taxable
                ? account.InterestRate * (1m - (Math.Clamp(account.TaxRate, 0m, 100m) / 100m))
                : account.InterestRate;
            return Math.Max(0m, account.Amount) * Math.Max(0m, netRate);
        }) / total;
    }

    private static int CalculateMonthsToGoalTaxAware(
        IReadOnlyList<AccountBalance> accounts,
        decimal goal,
        decimal monthlySurplus,
        IReserveInterestForecastService interestForecastService,
        int maxMonths = 240)
    {
        if (accounts.Sum(x => x.Amount) >= goal) return 0;
        var options = accounts.Select(x => new ReserveAccountOption(
            x.Id, x.Name, x.Amount, x.InterestRate, x.MonthlyContribution, true,
            x.TaxTreatment, x.TaxRate, x.TaxEffectiveFrom)).ToList();

        for (var month = 1; month <= maxMonths; month++)
        {
            var date = DateTime.Today.AddMonths(month);
            var accountProjection = interestForecastService.Build(options, date, includeFutureContributions: true).ProjectedTotal;
            var netRate = CalculateWeightedNetRate(accounts, date);
            var surplusProjection = new FinanceCalculator().CompoundMonthly(0m, netRate, monthlySurplus, month);
            if (accountProjection + surplusProjection >= goal) return month;
        }

        return -1;
    }

    private async Task<StatisticsForecastAssumption> BuildForecastAssumptionAsync()
    {
        var method = await repo.GetStringSettingAsync("ForecastMethod", "Last3Months");
        var requestedMonths = method switch
        {
            "LatestMonth" => 1,
            "Last6Months" => 6,
            _ => 3
        };
        var methodLabel = method switch
        {
            "LatestMonth" => "Latest completed month",
            "Last6Months" => "Last 6 completed months",
            _ => "Last 3 completed months"
        };

        var samples = new List<StatisticsForecastMonth>();
        var cursor = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

        // Search backwards for completed months that contain genuine monthly data.
        // Empty months are ignored so the default-income fallback cannot create a false surplus.
        for (var searched = 0; searched < 24 && samples.Count < requestedMonths; searched++, cursor = cursor.AddMonths(-1))
        {
            var income = await repo.GetIncomeAsync(cursor.Year, cursor.Month);
            var incomeEntries = await repo.GetMonthlyIncomeEntriesAsync(cursor.Year, cursor.Month);
            var hasEntries = income is not null || incomeEntries.Count > 0;

            foreach (var source in new[] { "bills", "everyday_spending", "extra_expenses", "investments", "savings" })
            {
                if ((await repo.GetRowsAsync(source, cursor.Month, cursor.Year)).Count > 0)
                {
                    hasEntries = true;
                    break;
                }
            }

            if (!hasEntries) continue;

            var totalResult = await repo.GetMonthResultAsync(cursor.Year, cursor.Month);
            var interestReceived = await repo.GetInterestIncomeTotalAsync(cursor.Year, cursor.Month);
            var operatingResult = Math.Round(totalResult - interestReceived, 2);

            samples.Add(new StatisticsForecastMonth(
                cursor.Year,
                cursor.Month,
                operatingResult,
                Math.Round(interestReceived, 2)));
        }

        var rawAverage = samples.Count == 0 ? 0m : samples.Average(x => x.Result);
        var monthlyContribution = Math.Round(Math.Max(0m, rawAverage), 2);
        var confidence = samples.Count switch
        {
            >= 6 => ("High confidence", "good"),
            >= 3 => ("Reasonable confidence", "good"),
            _ => ("Limited history", "warn")
        };

        return new StatisticsForecastAssumption(
            method,
            methodLabel,
            monthlyContribution,
            confidence.Item1,
            confidence.Item2,
            samples.Sum(x => x.InterestReceived),
            samples);
    }

    private sealed record StatisticsForecastAssumption(
        string Method,
        string MethodLabel,
        decimal MonthlyContribution,
        string Confidence,
        string ConfidenceCssClass,
        decimal InterestReceived,
        List<StatisticsForecastMonth> Months);


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGoalDate(DateTime goalDate)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() });
        var safeDate = goalDate == default ? new DateTime(DateTime.Today.Year + 1, 1, 31) : goalDate;
        await repo.SaveDecimalSettingAsync("StatisticsGoalYear", safeDate.Year);
        await repo.SaveDecimalSettingAsync("StatisticsGoalMonth", safeDate.Month);
        TempData["Success"] = "Statistics goal date updated.";
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
