using MoveInPlanner.Models.Entities;

namespace MoveInPlanner.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalItems { get; init; }
    public int PurchasedItems { get; init; }
    public int EssentialOutstanding { get; init; }
    public int ItemsWithoutChoices { get; init; }
    public decimal PlannedBudget { get; init; }
    public decimal PurchasedSpend { get; init; }

    /// <summary>
    /// Sum of every purchase option entered, including alternatives for the same household item.
    /// </summary>
    public decimal LoggedOptionsValue { get; init; }

    /// <summary>
    /// All preferred options per household item. When none are preferred, the cheapest logged option is used as the budget fallback.
    /// </summary>
    public decimal CurrentPlanValue { get; init; }

    public IReadOnlyList<CategoryDashboardGroup> Categories { get; init; } = [];
    public IReadOnlyList<HouseholdItem> NextActions { get; init; } = [];
    public decimal CompletionPercentage => TotalItems == 0 ? 0 : Math.Round((decimal)PurchasedItems / TotalItems * 100, 0);
}

public class CategoryDashboardGroup
{
    public required string Name { get; init; }
    public int Total { get; init; }
    public int Purchased { get; init; }
    public IReadOnlyList<HouseholdItem> Items { get; init; } = [];
}
