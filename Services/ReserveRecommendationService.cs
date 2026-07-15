using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IReserveRecommendationService
{
    List<ReserveRecoveryRecommendation> BuildRecoveryRecommendations(
        decimal availableAmount,
        IEnumerable<ReservePot> pots,
        IReadOnlyDictionary<int, ReservePotFundingSummary> summaries);
}

public sealed class ReserveRecommendationService : IReserveRecommendationService
{
    public List<ReserveRecoveryRecommendation> BuildRecoveryRecommendations(
        decimal availableAmount,
        IEnumerable<ReservePot> pots,
        IReadOnlyDictionary<int, ReservePotFundingSummary> summaries)
    {
        var remaining = Math.Max(0m, availableAmount);
        var recommendations = new List<ReserveRecoveryRecommendation>();

        foreach (var pot in pots.Where(x => x.IsActive).OrderBy(x => x.Priority).ThenBy(x => x.Name))
        {
            if (remaining <= 0m) break;
            if (!summaries.TryGetValue(pot.Id, out var summary)) continue;

            var outstanding = Math.Max(0m, summary.OutstandingRecovery);
            if (outstanding <= 0m) continue;

            var amount = Math.Min(outstanding, remaining);
            recommendations.Add(new ReserveRecoveryRecommendation
            {
                PotId = pot.Id,
                PotName = pot.Name,
                Priority = pot.Priority,
                OutstandingRecovery = outstanding,
                RecommendedAmount = amount
            });
            remaining -= amount;
        }

        return recommendations;
    }
}
