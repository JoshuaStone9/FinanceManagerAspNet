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

        foreach (var pot in pots.Where(x => x.IsActive)
                     .OrderByDescending(x => x.IsOverdrawn)
                     .ThenBy(x => x.Priority)
                     .ThenBy(x => x.Name))
        {
            if (remaining <= 0m) break;
            if (!summaries.TryGetValue(pot.Id, out var summary)) continue;

            var negativeRecovery = pot.NegativeBalanceRecoveryRequired;
            var fundingRecovery = Math.Max(0m, summary.OutstandingRecovery);
            var outstanding = negativeRecovery + fundingRecovery;
            if (outstanding <= 0m) continue;

            var amount = Math.Min(outstanding, remaining);
            recommendations.Add(new ReserveRecoveryRecommendation
            {
                PotId = pot.Id,
                PotName = pot.Name,
                Priority = pot.Priority,
                OutstandingRecovery = outstanding,
                NegativeBalanceRecovery = negativeRecovery,
                RecommendedAmount = amount
            });
            remaining -= amount;
        }

        return recommendations;
    }
}
