using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IMoneyPotActivityService
{
    Task<MoneyPotActivityViewModel> BuildAsync(int year, int month);
}

public sealed class MoneyPotActivityService(FinanceRepository repo) : IMoneyPotActivityService
{
    public async Task<MoneyPotActivityViewModel> BuildAsync(int year, int month)
    {
        var pots = await repo.GetReservePotsAsync();
        var contributions = await repo.GetRowsAsync("savings", month, year);
        var fundingSummaries = await repo.GetReservePotFundingSummariesAsync(pots);

        var activity = contributions
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new MoneyPotActivityRow
            {
                ContributionId = x.Id,
                PotId = x.ReservePotId,
                PotName = x.DisplayName,
                Amount = x.Amount,
                Date = x.Date,
                Notes = x.Notes
            })
            .ToList();

        var positiveContributions = activity.Where(x => x.Amount > 0m).ToList();
        var contributionByPot = positiveContributions
            .GroupBy(x => x.PotId.HasValue ? $"id:{x.PotId.Value}" : $"name:{x.PotName}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount), StringComparer.OrdinalIgnoreCase);

        var potRows = pots
            .Where(x => x.IsActive || contributionByPot.ContainsKey($"id:{x.Id}") || contributionByPot.ContainsKey($"name:{x.Name}"))
            .Select(pot =>
            {
                var contributed = contributionByPot.GetValueOrDefault($"id:{pot.Id}");
                if (contributed == 0m)
                    contributed = contributionByPot.GetValueOrDefault($"name:{pot.Name}");

                var summary = fundingSummaries.GetValueOrDefault(pot.Id);
                return new MoneyPotProgressRow
                {
                    PotId = pot.Id,
                    PotName = pot.Name,
                    CurrentBalance = pot.AllocatedAmount,
                    TargetAmount = pot.TargetAmount,
                    DueDate = pot.DueDate,
                    ContributedThisMonth = contributed,
                    RecentMonthlyContribution = summary?.RecentMonthlyContribution ?? pot.RecentMonthlyContribution,
                    EstimatedCompletionDate = summary?.EstimatedCompletionDate ?? pot.EstimatedCompletionDate,
                    IsPaused = pot.IsFundingPaused,
                    IsActive = pot.IsActive
                };
            })
            .OrderByDescending(x => x.ContributedThisMonth)
            .ThenBy(x => x.PotName)
            .ToList();

        var largest = positiveContributions.OrderByDescending(x => x.Amount).FirstOrDefault();
        var mostFunded = potRows.OrderByDescending(x => x.ContributedThisMonth).FirstOrDefault(x => x.ContributedThisMonth > 0m);
        var completedThisMonth = potRows.Count(x => x.TargetAmount.HasValue && x.CurrentBalance >= x.TargetAmount.Value && x.ContributedThisMonth > 0m);
        var atRiskCount = potRows.Count(x => x.IsBehindTarget);

        var insights = new List<string>();
        if (positiveContributions.Count == 0)
        {
            insights.Add("No Money Pot contributions were recorded during this month.");
        }
        else
        {
            insights.Add($"You added {positiveContributions.Sum(x => x.Amount):C} across {positiveContributions.Count} contribution{(positiveContributions.Count == 1 ? string.Empty : "s")}.");
            if (mostFunded is not null)
                insights.Add($"{mostFunded.PotName} received the most funding at {mostFunded.ContributedThisMonth:C}.");
            if (completedThisMonth > 0)
                insights.Add($"{completedThisMonth} pot{(completedThisMonth == 1 ? string.Empty : "s")} reached or passed its target this month.");
            if (atRiskCount > 0)
                insights.Add($"{atRiskCount} pot{(atRiskCount == 1 ? string.Empty : "s")} currently look likely to finish after their needed-by date.");
        }

        return new MoneyPotActivityViewModel
        {
            Year = year,
            Month = month,
            TotalAdded = positiveContributions.Sum(x => x.Amount),
            ContributionCount = positiveContributions.Count,
            LargestContributionAmount = largest?.Amount ?? 0m,
            LargestContributionPotName = largest?.PotName,
            MostFundedPotName = mostFunded?.PotName,
            MostFundedPotAmount = mostFunded?.ContributedThisMonth ?? 0m,
            CompletedThisMonth = completedThisMonth,
            PotsBehindTarget = atRiskCount,
            Activity = activity,
            Pots = potRows,
            Insights = insights
        };
    }
}
