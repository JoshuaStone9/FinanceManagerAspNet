using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IRecommendationApplicationService
{
    Task<ApplyRecommendationResult> ApplyAsync(int potId, decimal requestedAmount, string operationKey);
    Task<ApplyAllRecommendationsResult> ApplyAllAsync(string operationKey);
}

public sealed class RecommendationApplicationService(
    FinanceRepository repo,
    IReserveRecommendationService recommendationService) : IRecommendationApplicationService
{
    public async Task<ApplyRecommendationResult> ApplyAsync(
        int potId,
        decimal requestedAmount,
        string operationKey)
    {
        if (requestedAmount <= 0m)
        {
            return new ApplyRecommendationResult
            {
                PotId = potId,
                Message = "The recommendation amount must be greater than zero."
            };
        }

        var reserve = await repo.GetHouseholdReserveAsync();
        var pots = await repo.GetReservePotsAsync();
        var pot = pots.FirstOrDefault(x => x.Id == potId);

        if (pot is null || !pot.IsActive)
        {
            return new ApplyRecommendationResult
            {
                PotId = potId,
                Message = "The selected pot is no longer active or could not be found."
            };
        }

        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        var available = Math.Max(0m, reserve.Balance - pots.Where(x => x.IsActive).Sum(x => x.AllocatedAmount));
        var current = recommendationService
            .BuildRecoveryRecommendations(available, pots, summaries)
            .FirstOrDefault(x => x.PotId == potId);

        if (current is null || current.RecommendedAmount <= 0m)
        {
            return new ApplyRecommendationResult
            {
                PotId = potId,
                PotName = pot.Name,
                Message = "This recommendation is no longer available. The balances may have changed."
            };
        }

        var amount = Math.Min(requestedAmount, current.RecommendedAmount);
        return await repo.ApplyRecoveryRecommendationAsync(potId, amount, operationKey);
    }

    public async Task<ApplyAllRecommendationsResult> ApplyAllAsync(string operationKey)
    {
        var result = new ApplyAllRecommendationsResult();
        var reserve = await repo.GetHouseholdReserveAsync();
        var pots = await repo.GetReservePotsAsync();
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        var available = Math.Max(0m, reserve.Balance - pots.Where(x => x.IsActive).Sum(x => x.AllocatedAmount));
        var recommendations = recommendationService.BuildRecoveryRecommendations(available, pots, summaries);

        foreach (var recommendation in recommendations.OrderBy(x => x.Priority).ThenBy(x => x.PotName))
        {
            var itemKey = $"{operationKey}:{recommendation.PotId}";
            var applied = await repo.ApplyRecoveryRecommendationAsync(
                recommendation.PotId,
                recommendation.RecommendedAmount,
                itemKey);

            result.Results.Add(applied);
            if (applied.Succeeded && !applied.WasAlreadyApplied)
            {
                result.AppliedCount++;
                result.TotalApplied += applied.AppliedAmount;
            }
        }

        return result;
    }
}
