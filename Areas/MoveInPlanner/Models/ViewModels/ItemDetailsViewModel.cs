using MoveInPlanner.Models.Entities;

namespace MoveInPlanner.Models.ViewModels;

public class ItemDetailsViewModel
{
    public required HouseholdItem Item { get; init; }

    /// <summary>
    /// The combined value of every purchase option currently logged for this item.
    /// This intentionally includes alternatives, so it is a research-total rather than a planned spend figure.
    /// </summary>
    public decimal LoggedOptionsValue { get; init; }

    public decimal? CheapestOptionValue { get; init; }
    public decimal? HighestOptionValue { get; init; }
    /// <summary>
    /// Combined value of every purchase option included in the preferred plan.
    /// Each option uses Price × Quantity.
    /// </summary>
    public decimal? PreferredOptionValue { get; init; }

    public decimal? PurchasedChoicesValue { get; init; }

    public decimal PurchasedValue { get; init; }

    public int PreferredOptionCount { get; init; }
}
