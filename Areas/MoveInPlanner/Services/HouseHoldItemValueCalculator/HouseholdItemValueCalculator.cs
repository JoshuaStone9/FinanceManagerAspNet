using MoveInPlanner.Models.Entities;
using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Services;

public static class HouseholdItemValueCalculator
{
    private static decimal ChoiceValue(ProductChoice choice) => choice.Price * choice.Quantity;

    public static decimal? CheapestOptionValue(HouseholdItem item) => item.ProductChoices.Count == 0 ? null : item.ProductChoices.Min(ChoiceValue);

    public static decimal? PreferredPlanValue(HouseholdItem item)
    {
        var preferredChoices = item.ProductChoices
            .Where(choice => choice.IsPreferred)
            .ToList();

        return preferredChoices.Count == 0 ? null : preferredChoices.Sum(ChoiceValue);
    }

    public static decimal RemainingPlanValue(HouseholdItem item)
    {
        if (item.Status == PurchaseStatus.Purchased)
            return 0m;

        return CurrentPlanValue(item);
    }

    public static decimal? PurchasedChoicesValue(HouseholdItem item)
    {
        var purchasedChoices = item.ProductChoices
            .Where(choice => choice.IsPurchased)
            .ToList();

        return purchasedChoices.Count == 0 ? null : purchasedChoices.Sum(ChoiceValue);
    }

    public static decimal CurrentPlanValue(HouseholdItem item)
    {
        if (item.Status == PurchaseStatus.Purchased)
            return PurchaseValue(item);

        var preferredPlanValue = PreferredPlanValue(item);

        if (preferredPlanValue.HasValue)
            return preferredPlanValue.Value;

        return CheapestOptionValue(item) ?? 0;
    }

    public static decimal PurchasedValue(HouseholdItem item) =>
        item.Status == PurchaseStatus.Purchased ? PurchaseValue(item) : 0;

    public static decimal LoggedOptionsValue(HouseholdItem item) => item.ProductChoices.Sum(ChoiceValue);

    private static decimal PurchaseValue(HouseholdItem item) =>
        item.ActualPurchasePrice
        ?? PurchasedChoicesValue(item)
        ?? PreferredPlanValue(item)
        ?? 0;
}
