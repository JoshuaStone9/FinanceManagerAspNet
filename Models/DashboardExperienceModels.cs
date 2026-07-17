namespace FinanceManagerAspNet.Models;

public sealed class DashboardExperience
{
    public MonthlyMoneyJourney Journey { get; init; } = new();
    public IReadOnlyList<DashboardSectionSummary> MonthlySections { get; init; } = [];
    public IReadOnlyList<DashboardActionItem> UpcomingActions { get; init; } = [];
    public IReadOnlyList<DashboardActivityItem> RecentActivity { get; init; } = [];
    public decimal AllocatedToFuture { get; init; }
    public int RecordedItemCount { get; init; }
}

public sealed class MonthlyMoneyJourney
{
    public decimal Income { get; init; }
    public decimal CarryForward { get; init; }
    public decimal EffectiveIncome => Income + CarryForward;
    public decimal EssentialBills { get; init; }
    public decimal EverydaySpending { get; init; }
    public decimal ExtraExpenses { get; init; }
    public decimal AvailableBeforeFutureAllocations { get; init; }
    public decimal Investments { get; init; }
    public decimal HouseholdReserveAllocations { get; init; }
    public decimal Remaining { get; init; }
    public bool IsOverBudget => Remaining < 0;
}

public sealed record DashboardSectionSummary(
    string Key,
    string Title,
    decimal Amount,
    int ItemCount,
    string Description,
    string Anchor,
    string Icon,
    decimal? SecondaryAmount = null,
    string? SecondaryLabel = null);

public sealed record DashboardActionItem(
    string Title,
    string Detail,
    string Url,
    string Severity,
    DateTime? DueDate = null);

public sealed record DashboardActivityItem(
    string Title,
    string Detail,
    DateTime OccurredAt,
    string Icon,
    decimal? Amount = null);
