using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IMonthlyFundingReviewService
{
    Task<MonthlyFundingReview> BuildAsync(int year, int month);
}

public sealed class MonthlyFundingReviewService(FinanceRepository repo) : IMonthlyFundingReviewService
{
    public async Task<MonthlyFundingReview> BuildAsync(int year, int month)
    {
        var reviewStart = new DateTime(year, month, 1);
        var reviewEnd = reviewStart.AddMonths(1).AddDays(-1);
        var pots = await repo.GetReservePotsAsync();
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        var events = await repo.GetFinanceEventsAsync(from: reviewStart, to: reviewEnd);
        var allReminders = await repo.GetFinanceRemindersAsync("All");

        var rows = pots
            .Where(pot => pot.IsActive || summaries.TryGetValue(pot.Id, out var summary) &&
                summary.Months.Any(x => x.Year == year && x.Month == month))
            .OrderBy(pot => pot.Priority)
            .ThenBy(pot => pot.Name)
            .Select(pot => BuildPotRow(pot, summaries.GetValueOrDefault(pot.Id), year, month))
            .ToList();

        var reminders = allReminders
            .Where(x => x.CreatedAt >= reviewStart && x.CreatedAt < reviewStart.AddMonths(1))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var recommendationEvents = events
            .Where(x => string.Equals(x.EventType, "RecoveryRecommendationApplied", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new MonthlyFundingReview
        {
            Year = year,
            Month = month,
            PlannedTotal = rows.Sum(x => x.PlannedAmount),
            ActualTotal = rows.Sum(x => x.ActualAmount),
            EffectiveFundingTotal = rows.Sum(x => x.EffectiveFunding),
            RemainingTotal = rows.Sum(x => x.RemainingAmount),
            RecoveryAppliedTotal = rows.Sum(x => x.RecoveryApplied),
            GenuineExcessTotal = rows.Sum(x => x.GenuineExcess),
            CarriedExcessUsedTotal = rows.Sum(x => x.CarriedExcessUsed),
            CarriedExcessCreatedTotal = rows.Sum(x => x.CarriedExcessCreated),
            ShortfallTotal = rows.Sum(x => x.ShortfallAmount),
            ReminderCount = reminders.Count,
            RecommendationApplicationCount = recommendationEvents.Count,
            RecommendationApplicationTotal = recommendationEvents.Sum(x => x.Amount ?? 0m),
            PotRows = rows,
            Events = events,
            Reminders = reminders
        };
    }

    private static MonthlyFundingReviewPotRow BuildPotRow(
        ReservePot pot,
        ReservePotFundingSummary? summary,
        int year,
        int month)
    {
        var fundingMonth = summary?.Months.FirstOrDefault(x => x.Year == year && x.Month == month);
        if (fundingMonth is null)
        {
            return new MonthlyFundingReviewPotRow
            {
                PotId = pot.Id,
                PotName = pot.Name,
                Priority = pot.Priority,
                IsActive = pot.IsActive,
                Status = pot.IsActive ? "Not scheduled" : "Inactive"
            };
        }

        return new MonthlyFundingReviewPotRow
        {
            PotId = pot.Id,
            PotName = pot.Name,
            Priority = pot.Priority,
            IsActive = pot.IsActive,
            IsPaused = fundingMonth.IsPaused,
            PlannedAmount = fundingMonth.ExpectedAmount,
            ActualAmount = fundingMonth.ActualAmount,
            EffectiveFunding = fundingMonth.EffectiveCurrentMonthFunding,
            RemainingAmount = Math.Max(0m, fundingMonth.ExpectedAmount - fundingMonth.EffectiveCurrentMonthFunding),
            RecoveryApplied = fundingMonth.AppliedToRecovery,
            GenuineExcess = fundingMonth.GenuineExcess,
            CarriedExcessUsed = fundingMonth.CarriedExcessUsed,
            CarriedExcessCreated = fundingMonth.CarriedExcessCreated,
            ShortfallAmount = fundingMonth.ShortfallAmount,
            Status = fundingMonth.Status
        };
    }
}
