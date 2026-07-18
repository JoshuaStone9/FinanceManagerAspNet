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
}
