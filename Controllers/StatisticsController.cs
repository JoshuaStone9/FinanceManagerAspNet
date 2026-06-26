using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class StatisticsController(FinanceRepository repo, FinanceCalculator calc, IConfiguration config) : Controller
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
        var monthlyTarget = decimal.TryParse(config["FinanceSettings:MonthlySavingTarget"], out var mt) ? mt : 1200m;
        var target = new DateTime(DateTime.Today.Year + (DateTime.Today.Month > 4 ? 1 : 0), 4, 30);
        var months = Math.Max(0, ((target.Year - DateTime.Today.Year) * 12) + target.Month - DateTime.Today.Month);

        var included = accounts.Where(a => a.IncludeInGlobalGoal).ToList();
        var total = included.Sum(a => a.Amount);
        var averageIncome = income.Count == 0 ? 3500m : income.Average(x => x.Amount);
        var monthlyContrib = included.Sum(x => x.MonthlyContribution);
        var projections = calc.ProjectAccountsDetailed(included, months);
        var projectedSalarySavings = calc.ProjectSalarySavings(monthlyTarget, months);
        var projectedWithoutInterest = calc.ProjectAccountsWithoutInterest(included, months, monthlyTarget);
        var projectedWithInterest = calc.ProjectAccounts(included, months, monthlyTarget);
        var monthsToGoalWithInterest = calc.MonthsToGoalWithInterest(included, goal, monthlyTarget);

        var allocatedToSavingPots = await repo.GetTotalAllocatedToSavingPotsAsync();

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
            AllocatedToSavingPots = allocatedToSavingPots
        };

        return View(vm);
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
