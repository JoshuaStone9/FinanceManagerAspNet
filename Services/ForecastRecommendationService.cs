using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IForecastRecommendationService
{
    IReadOnlyList<ForecastRecommendation> Build(MonthlyCashFlowForecastResult cashFlow, FinancialForecastResult goals);
}

public sealed class ForecastRecommendationService : IForecastRecommendationService
{
    public IReadOnlyList<ForecastRecommendation> Build(MonthlyCashFlowForecastResult cashFlow, FinancialForecastResult goals)
    {
        var recommendations = new List<ForecastRecommendation>();

        foreach (var month in cashFlow.Months.Where(x => x.RemainingCash < 0m).Take(3))
        {
            recommendations.Add(new ForecastRecommendation
            {
                Type = ForecastRecommendationType.CashShortfall,
                Severity = ForecastRecommendationSeverity.Critical,
                Title = $"{month.Month:MMMM} is forecast to go negative",
                Description = $"The projected shortfall is {Math.Abs(month.RemainingCash):C}. Review discretionary spending, investments or reserve allocations before this month.",
                SuggestedAmount = Math.Abs(month.RemainingCash), Month = month.Month
            });
        }

        var lowBuffer = cashFlow.Months.FirstOrDefault(x => x.RemainingCash >= 0m && x.RemainingCash < 200m);
        if (lowBuffer is not null)
            recommendations.Add(new ForecastRecommendation { Type = ForecastRecommendationType.LowCashBuffer, Severity = ForecastRecommendationSeverity.Warning, Title = $"Low cash buffer in {lowBuffer.Month:MMMM}", Description = $"Only {lowBuffer.RemainingCash:C} is forecast to remain. Aim to retain at least £200 of flexibility.", SuggestedAmount = 200m - lowBuffer.RemainingCash, Month = lowBuffer.Month });

        foreach (var pot in goals.Pots.Where(x => x.Status is ForecastGoalStatus.AtRisk or ForecastGoalStatus.Behind).Take(4))
            recommendations.Add(new ForecastRecommendation { Type = ForecastRecommendationType.GoalRisk, Severity = ForecastRecommendationSeverity.Warning, Title = $"{pot.PotName} needs attention", Description = pot.Explanation, SuggestedAmount = pot.AdditionalMonthlyContributionRequired, PotId = pot.PotId, PotName = pot.PotName });

        foreach (var pot in goals.Pots.Where(x => x.Status == ForecastGoalStatus.Overdrawn).Take(2))
            recommendations.Add(new ForecastRecommendation { Type = ForecastRecommendationType.OverdrawnPot, Severity = ForecastRecommendationSeverity.Critical, Title = $"Restore {pot.PotName}", Description = pot.RecommendedAction, SuggestedAmount = Math.Abs(pot.CurrentBalance), PotId = pot.PotId, PotName = pot.PotName });

        var last = cashFlow.Months.LastOrDefault();
        if (last is not null && last.RemainingCash >= 500m && recommendations.All(x => x.Type != ForecastRecommendationType.CashShortfall))
            recommendations.Add(new ForecastRecommendation { Type = ForecastRecommendationType.ForecastSurplus, Severity = ForecastRecommendationSeverity.Opportunity, Title = "Forecast surplus available", Description = $"The plan leaves {last.RemainingCash:C} by {last.Month:MMMM yyyy}. Consider assigning part of it to a priority goal while retaining a cash buffer.", SuggestedAmount = Math.Max(0m, last.RemainingCash - 200m), Month = last.Month });

        if (recommendations.Count == 0)
            recommendations.Add(new ForecastRecommendation { Type = ForecastRecommendationType.CompletedGoal, Severity = ForecastRecommendationSeverity.Information, Title = "No immediate forecast actions", Description = "The current cash-flow plan and tracked goals do not show an urgent issue." });

        return recommendations;
    }
}
