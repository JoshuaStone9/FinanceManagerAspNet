using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IDashboardSummaryService
{
    Task<DashboardIntelligenceSummary> BuildAsync(
        HouseholdReserve reserve,
        IReadOnlyList<ReservePot> pots);
}

public sealed class DashboardSummaryService(
    FinanceRepository repo,
    IReserveRecommendationService recommendationService) : IDashboardSummaryService
{
    public async Task<DashboardIntelligenceSummary> BuildAsync(
        HouseholdReserve reserve,
        IReadOnlyList<ReservePot> pots)
    {
        var activePots = pots.Where(x => x.IsActive).ToList();
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        await repo.SyncFundingRemindersAsync(pots, summaries);

        var allocated = activePots.Sum(x => x.AllocatedAmount);
        var availableReserve = Math.Max(0m, reserve.Balance - allocated);
        var recommendations = recommendationService.BuildRecoveryRecommendations(
            availableReserve,
            activePots,
            summaries);
        var dueReminders = await repo.GetFinanceRemindersAsync("Open", dueOnly: true);

        var today = DateTime.Today;
        var upcomingTargets = activePots
            .Where(x => x.DueDate.HasValue && x.TargetAmount.HasValue && x.AllocatedAmount < x.TargetAmount.Value)
            .Where(x => x.DueDate!.Value.Date >= today)
            .OrderBy(x => x.DueDate)
            .Take(5)
            .Select(x => new DashboardUpcomingTarget(
                x.Id,
                x.Name,
                x.DueDate!.Value.Date,
                Math.Max(0m, x.TargetAmount!.Value - x.AllocatedAmount)))
            .ToList();

        var currentSummaries = activePots
            .Select(x => summaries.TryGetValue(x.Id, out var summary) ? summary : null)
            .Where(x => x is not null)
            .Cast<ReservePotFundingSummary>()
            .ToList();

        var completedCount = activePots.Count(x => x.TargetAmount.HasValue && x.AllocatedAmount >= x.TargetAmount.Value);
        var pausedCount = activePots.Count(x => x.IsFundingPaused);
        var overdueOrMissedCount = currentSummaries.Count(x => x.CurrentStatus is "Overdrawn" or "Overdue" or "Missed");
        var partiallyFundedCount = currentSummaries.Count(x => x.CurrentStatus == "Partially funded");
        var unhealthyIds = activePots
            .Where(x => x.IsFundingPaused || (x.TargetAmount.HasValue && x.AllocatedAmount >= x.TargetAmount.Value))
            .Select(x => x.Id)
            .ToHashSet();
        foreach (var pot in activePots)
        {
            if (summaries.TryGetValue(pot.Id, out var summary) &&
                (summary.CurrentStatus is "Overdrawn" or "Overdue" or "Missed" or "Partially funded"))
            {
                unhealthyIds.Add(pot.Id);
            }
        }

        return new DashboardIntelligenceSummary
        {
            ActivePotCount = activePots.Count,
            MonthlyPlanned = activePots
                .Where(x => !x.IsFundingPaused)
                .Sum(x => x.IntendedMonthlyContribution),
            FundedThisMonth = currentSummaries.Sum(x => x.CurrentMonthEffectiveFunding),
            RemainingThisMonth = currentSummaries.Sum(x => x.CurrentMonthRemaining),
            DueReminderCount = dueReminders.Count,
            OverdueOrMissedCount = overdueOrMissedCount,
            PartiallyFundedCount = partiallyFundedCount,
            PausedCount = pausedCount,
            CompletedCount = completedCount,
            HealthyCount = Math.Max(0, activePots.Count - unhealthyIds.Count),
            ApproachingTargetCount = upcomingTargets.Count(x => x.DaysRemaining <= 30),
            AvailableReserve = availableReserve,
            RecommendedAllocationTotal = recommendations.Sum(x => x.RecommendedAmount),
            Recommendations = recommendations,
            DueReminders = dueReminders.Take(3).ToList(),
            UpcomingTargets = upcomingTargets
        };
    }
}
