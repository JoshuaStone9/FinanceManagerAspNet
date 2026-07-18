namespace FinanceManagerAspNet.Models;

public sealed record MonthlyIncomeEntry(
    int Id,
    string Name,
    decimal Amount,
    DateTime Date,
    string? Category,
    string? Notes,
    bool IsRecurring);

public sealed class MonthlyIncomeWorkspaceViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public IReadOnlyList<MonthlyIncomeEntry> Entries { get; init; } = [];
    public IReadOnlyList<MonthlyIncomeEntry> MissingRecurringEntries { get; init; } = [];
    public DateTime MonthStart => new(Year, Month, 1);
    public DateTime PreviousMonth => MonthStart.AddMonths(-1);
    public DateTime NextMonth => MonthStart.AddMonths(1);
    public decimal Total => Entries.Sum(x => x.Amount);
    public decimal Average => Entries.Count == 0 ? 0 : Total / Entries.Count;
    public int RecurringCount => Entries.Count(x => x.IsRecurring);
    public IReadOnlyList<PassiveIncomeEstimate> PassiveIncomeEstimates { get; init; } = [];
    public decimal EstimatedPassiveIncome => PassiveIncomeEstimates.Where(x => !x.IsReceived).Sum(x => x.EstimatedAmount);
    public decimal ReceivedPassiveIncome => PassiveIncomeEstimates.Where(x => x.IsReceived).Sum(x => x.ActualAmount ?? 0m);
}


public sealed record PassiveIncomeEstimate(
    string SourceKey,
    string SourceName,
    decimal Balance,
    decimal AnnualInterestRate,
    decimal EstimatedAmount,
    decimal? ActualAmount,
    DateTime? ReceivedDate,
    int? IncomeEntryId)
{
    public bool IsReceived => ActualAmount.HasValue;
    public decimal Difference => IsReceived ? ActualAmount!.Value - EstimatedAmount : 0m;
}

public sealed record PassiveIncomeRecord(
    int Id,
    string SourceKey,
    string SourceName,
    string IncomeType,
    int Year,
    int Month,
    decimal EstimatedAmount,
    decimal ActualAmount,
    DateTime ReceivedDate,
    int? IncomeEntryId,
    string? Notes);
