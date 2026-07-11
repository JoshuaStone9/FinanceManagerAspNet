using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class SavingPotsController(FinanceRepository repo) : Controller
{
    public async Task<IActionResult> Index(int? year, int? forecastYear, int? forecastMonth)
    {
        var selectedYear = year ?? DateTime.Today.Year;
        var savedEmergencyFund = await repo.GetEmergencyFundAsync();
        var savingsSources = await repo.GetAccountsAsync(savedEmergencyFund);
        var selectedSources = savingsSources.Where(a => a.IncludeInSavingsCommand).ToList();
        var grossSelectedFund = selectedSources.Sum(a => a.Amount);
        var reservedFunds = await repo.GetReservedFundsAsync();
        var reservedDeduction = reservedFunds.Where(r => r.DeductFromSavingsAllocation).Sum(r => r.Amount);
        var emergencyFund = Math.Max(0, grossSelectedFund - reservedDeduction);
        var emergencyUpdated = selectedSources.Count == 0 ? await repo.GetEmergencyFundUpdatedAsync() : selectedSources.Max(a => a.UpdatedAt);
        var interestRate = selectedSources.Count == 0
            ? await repo.GetDecimalSettingAsync("EmergencyFundInterestRate", 3.8m)
            : Math.Round(selectedSources.Sum(a => a.Amount * a.InterestRate) / Math.Max(1, selectedSources.Sum(a => a.Amount)), 4);
        var emergencyBaseline = await repo.GetDecimalSettingAsync("EmergencyFundBaseline", 12000m);
        var monthlySavingRate = await repo.GetDecimalSettingAsync("SavingsMonthlyRate", await repo.GetDecimalSettingAsync("MonthlySavingTarget", 1200m));
        var targetYear = (int)await repo.GetDecimalSettingAsync("SavingsOverallTargetYear", 2027m);
        var targetMonth = (int)await repo.GetDecimalSettingAsync("SavingsOverallTargetMonth", 4m);
        var overallTarget = new DateTime(Math.Clamp(targetYear, 2000, 2100), Math.Clamp(targetMonth, 1, 12), 1);
        var selectedForecastDate = new DateTime(
            Math.Clamp(forecastYear ?? overallTarget.Year, 2000, 2100),
            Math.Clamp(forecastMonth ?? overallTarget.Month, 1, 12),
            1);

        var pots = await repo.GetSavingPotsAsync();
        var months = await repo.GetSavingPotMonthsAsync(selectedYear);
        var contributionSchedule = await repo.GetSavingsContributionChangesAsync();

        var emergencyBaselineComplete = emergencyFund >= emergencyBaseline;
        var emergencyShortfall = Math.Max(0, emergencyBaseline - emergencyFund);
        int? monthsUntilEmergencyBaseline = emergencyShortfall <= 0
            ? 0
            : monthlySavingRate <= 0 ? null : (int)Math.Ceiling(emergencyShortfall / monthlySavingRate);

        var availableOverflow = emergencyBaselineComplete ? Math.Max(0, emergencyFund - emergencyBaseline) : 0m;
        var remainingOverflow = availableOverflow;

        var orderedPots = pots
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.TargetDate ?? DateTime.MaxValue)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        var rows = new List<SavingPotRowViewModel>();
        int? cumulativeMonthsBeforePot = emergencyBaselineComplete ? 0 : monthsUntilEmergencyBaseline;

        var fundingQueuePosition = 0;

        foreach (var p in orderedPots)
        {
            fundingQueuePosition++;
            var manual = months.Where(m => m.SavingPotId == p.Id && m.IsSaved).Sum(m => m.SavedAmount);
            var remainingPotTargetBeforeAutoAllocation = Math.Max(0, p.TargetAmount - manual);
            var autoAllocation = emergencyBaselineComplete && !p.IsPaused
                ? Math.Min(remainingOverflow, remainingPotTargetBeforeAutoAllocation)
                : 0m;

            if (emergencyBaselineComplete)
            {
                remainingOverflow -= autoAllocation;
            }

            var monthlyAfterBaseline = p.IsPaused || p.IsOneOffFundingOnly
                ? 0m
                : p.UsesManualContribution ? p.MonthlyAmount : p.UsesGlobalContribution ? monthlySavingRate : 0m;

            var row = new SavingPotRowViewModel
            {
                Pot = p,
                Months = months.Where(m => m.SavingPotId == p.Id).ToList(),
                ManualSavedBalance = manual,
                AutoAllocatedBalance = autoAllocation,
                ForecastMonthlyAmount = emergencyBaselineComplete && cumulativeMonthsBeforePot == 0 && remainingPotTargetBeforeAutoAllocation > autoAllocation ? monthlyAfterBaseline : 0m,
                ForecastMonthlyAfterBaseline = monthlyAfterBaseline,
                EmergencyBaselineComplete = emergencyBaselineComplete,
                EmergencyBaselineMonthsRemaining = cumulativeMonthsBeforePot,
                FundingQueuePosition = fundingQueuePosition
            };

            rows.Add(row);

            if (!row.IsComplete)
            {
                if (cumulativeMonthsBeforePot.HasValue && monthlyAfterBaseline > 0)
                {
                    cumulativeMonthsBeforePot += (int)Math.Ceiling(row.Remaining / monthlyAfterBaseline);
                }
                else
                {
                    cumulativeMonthsBeforePot = null;
                }
            }
        }

        var allocated = rows.Sum(r => r.SavedBalance);
        var monthsToOverallTarget = Math.Max(0, ((overallTarget.Year - DateTime.Today.Year) * 12) + overallTarget.Month - DateTime.Today.Month);
        var projectedEmergency = ProjectBalanceWithSchedule(emergencyFund, monthlySavingRate, contributionSchedule, interestRate, monthsToOverallTarget);
        var monthsToForecastDate = Math.Max(0, ((selectedForecastDate.Year - DateTime.Today.Year) * 12) + selectedForecastDate.Month - DateTime.Today.Month);
        var projectedFundAtForecastDate = ProjectBalanceWithSchedule(emergencyFund, monthlySavingRate, contributionSchedule, interestRate, monthsToForecastDate);
        var forecastPots = BuildForecastPotsAtDate(orderedPots, months, projectedFundAtForecastDate, emergencyBaseline);

        var vm = new SavingPotsViewModel
        {
            Year = selectedYear,
            EmergencyFundTotal = emergencyFund,
            GrossSelectedFundTotal = grossSelectedFund,
            ReservedFunds = reservedFunds,
            EmergencyBaseline = emergencyBaseline,
            AvailableOverflow = availableOverflow,
            AllocatedToPots = allocated,
            Pots = rows,
            InterestRate = interestRate,
            MonthlySavingRate = monthlySavingRate,
            LastUpdated = emergencyUpdated,
            OverallTargetDate = overallTarget,
            ProjectedEmergencyByTarget = projectedEmergency,
            ForecastDate = selectedForecastDate,
            ForecastProjectedFundTotal = projectedFundAtForecastDate,
            ForecastProjectedPotProgress = forecastPots.Sum(p => p.ProjectedAllocated),
            ForecastPots = forecastPots,
            SavingsSources = savingsSources,
            ContributionSchedule = contributionSchedule,
            Recommendations = BuildRecommendations(emergencyFund, emergencyBaseline, availableOverflow, remainingOverflow, rows, monthlySavingRate, interestRate)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBrain(decimal emergencyBaseline, decimal monthlySavingRate, int targetYear, int targetMonth, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        var target = new DateTime(Math.Clamp(targetYear, 2000, 2100), Math.Clamp(targetMonth, 1, 12), 1);
        await repo.SaveSavingsBrainSettingsAsync(Math.Max(0, emergencyBaseline), Math.Max(0, monthlySavingRate), target);
        return RedirectToAction(nameof(Index), new { year });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSavingsSources(bool includeEmergencyFund, int[] accountIds, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveSavingsSourcesAsync(includeEmergencyFund, accountIds);
        return RedirectToAction(nameof(Index), new { year });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualFundUpdate(decimal newTotal, string? reason, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveManualEmergencyFundUpdateAsync(Math.Max(0, newTotal), reason);
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContributionChange(DateTime startsOn, decimal monthlyAmount, string? note, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.AddSavingsContributionChangeAsync(startsOn == default ? DateTime.Today : startsOn, Math.Max(0, monthlyAmount), note);
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContributionChange(int id, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeleteSavingsContributionChangeAsync(id);
        return RedirectToAction(nameof(Index), new { year });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReservedFund(int id, string name, decimal amount, string category, string accessSpeed, bool includeInNetWorth, bool deductFromSavingsAllocation, string? notes, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveReservedFundAsync(id, name, amount, category, accessSpeed, includeInNetWorth, deductFromSavingsAllocation, notes);
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReservedFund(int id, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeleteReservedFundAsync(id);
        return RedirectToAction(nameof(Index), new { year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePot(int id, string name, decimal targetAmount, decimal monthlyAmount, string contributionMode, int priority, string potType, string accessSpeed, decimal interestRate, DateTime? targetDate, string? destination, int year)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveSavingPotAsync(id, name, Math.Max(0, targetAmount), Math.Max(0, monthlyAmount), contributionMode, Math.Max(1, priority), potType, accessSpeed, Math.Max(0, interestRate), targetDate, destination);
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

    private static decimal ProjectBalance(decimal start, decimal monthly, decimal annualRate, int months)
    {
        var balance = start;
        var monthlyRate = annualRate / 100m / 12m;
        for (var i = 0; i < months; i++)
        {
            balance += monthly;
            balance += balance * monthlyRate;
        }
        return Math.Round(balance, 2);
    }

    private static decimal ProjectBalanceWithSchedule(decimal start, decimal defaultMonthly, List<SavingsContributionChange> schedule, decimal annualRate, int months)
    {
        var balance = start;
        var monthlyRate = annualRate / 100m / 12m;
        var orderedChanges = schedule.OrderBy(c => c.StartsOn).ToList();

        for (var i = 0; i < months; i++)
        {
            var monthDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(i + 1);
            var activeMonthly = orderedChanges.LastOrDefault(c => c.StartsOn <= monthDate)?.MonthlyAmount ?? defaultMonthly;
            balance += activeMonthly;
            balance += balance * monthlyRate;
        }

        return Math.Round(balance, 2);
    }

    private static List<SavingPotForecastAtDate> BuildForecastPotsAtDate(List<SavingPot> orderedPots, List<SavingPotMonth> months, decimal projectedFund, decimal emergencyBaseline)
    {
        var projectedOverflow = Math.Max(0, projectedFund - emergencyBaseline);
        var remainingOverflow = projectedOverflow;
        var forecast = new List<SavingPotForecastAtDate>();

        foreach (var pot in orderedPots)
        {
            var manual = months.Where(m => m.SavingPotId == pot.Id && m.IsSaved).Sum(m => m.SavedAmount);
            var remaining = Math.Max(0, pot.TargetAmount - manual);
            var auto = pot.IsPaused ? 0m : Math.Min(remainingOverflow, remaining);
            remainingOverflow -= auto;

            forecast.Add(new SavingPotForecastAtDate
            {
                Name = pot.Name,
                Priority = pot.Priority,
                TargetDate = pot.TargetDate,
                TargetAmount = pot.TargetAmount,
                ProjectedAllocated = manual + auto
            });
        }

        return forecast;
    }

    private static List<SavingsRecommendation> BuildRecommendations(decimal emergencyFund, decimal baseline, decimal overflow, decimal unallocated, List<SavingPotRowViewModel> pots, decimal monthlySavingRate, decimal emergencyRate)
    {
        var list = new List<SavingsRecommendation>();
        if (emergencyFund < baseline)
        {
            list.Add(new SavingsRecommendation { Stars = 5, Tone = "warning", Title = "Rebuild emergency fund first", Detail = $"Your emergency fund is £{baseline - emergencyFund:N2} below the £{baseline:N0} baseline. Direct new savings here before funding other pots." });
            return list;
        }

        list.Add(new SavingsRecommendation { Stars = 5, Tone = "good", Title = "Emergency fund complete", Detail = $"Your first £{baseline:N0} is protected as instant-access cash. Overflow can now be allocated automatically by priority." });

        var nextPot = pots.FirstOrDefault(p => !p.IsComplete);
        if (nextPot is not null)
        {
            list.Add(new SavingsRecommendation { Stars = 5, Tone = "info", Title = $"Next £1 should go to {nextPot.Pot.Name}", Detail = $"This is priority {nextPot.Pot.Priority}. It needs £{nextPot.Remaining:N2} more and is marked as {nextPot.Pot.AccessSpeed} access." });
        }
        else if (overflow > 0)
        {
            list.Add(new SavingsRecommendation { Stars = 4, Tone = "good", Title = "All named pots are funded", Detail = "New overflow can be directed to a money market fund, cash ISA, premium bonds or investing depending on access needs." });
        }

        if (unallocated > 0)
        {
            list.Add(new SavingsRecommendation { Stars = 4, Tone = "warning", Title = "Idle overflow detected", Detail = $"£{unallocated:N2} is above your emergency baseline and not assigned to a named pot yet." });
        }

        if (emergencyRate > 0 && overflow > 0)
        {
            var extraInterest = Math.Round(overflow * emergencyRate / 100m, 2);
            list.Add(new SavingsRecommendation { Stars = 3, Tone = "info", Title = "Passive income estimate", Detail = $"At {emergencyRate:N2}% AER, the current overflow could generate roughly £{extraInterest:N2}/year before tax if held in a similar low-risk cash product." });
        }

        var behind = pots.Where(p => p.TargetStatus == "Behind target").ToList();
        if (behind.Count > 0)
        {
            var firstBehind = behind.First();
            list.Add(new SavingsRecommendation
            {
                Stars = 4,
                Tone = "warning",
                Title = $"{behind.Count} pot(s) behind target",
                Detail = $"{firstBehind.Pot.Name} needs around {firstBehind.RequiredMonthlyToTargetDate?.ToString("C") ?? "more per month"} to hit its target date. Review saving pace, priority or target date."
            });
        }

        if (monthlySavingRate > 0 && nextPot is not null)
        {
            var months = (int)Math.Ceiling(nextPot.Remaining / monthlySavingRate);
            list.Add(new SavingsRecommendation { Stars = 3, Tone = "info", Title = "Current saving pace", Detail = $"At £{monthlySavingRate:N0}/month, {nextPot.Pot.Name} could be complete in about {months} month(s) once it is actively receiving funding." });
        }

        return list;
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;

    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
