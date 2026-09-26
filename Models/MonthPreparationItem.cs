namespace FinanceManagerAspNet.Models;

public sealed record MonthPreparationItem(
    string Source, int Id, string Name, decimal Amount, DateTime Date,
    bool IsRecurring, string? Category = null, string? Type = null,
    string? Length = null, string? Notes = null, int? AccountBalanceId = null,
    int? ReservePotId = null)
{
    public string Key => $"{Source}:{Id}";

    public DateTime DateIn(DateTime month) =>
        new(month.Year, month.Month, Math.Min(Date.Day, DateTime.DaysInMonth(month.Year, month.Month)));

    // Monthly preparation treats an entry name as its identity within a section,
    // matching the existing recurring setup's duplicate prevention rules.
    public static IReadOnlyList<MonthPreparationItem> Missing(
        IEnumerable<MonthPreparationItem> previous, IEnumerable<string> existingNames)
    {
        var existing = existingNames.Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return previous.Where(x => !existing.Contains(x.Name.Trim()))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).First())
            .OrderByDescending(x => x.IsRecurring).ThenBy(x => x.Name)
            .ToList();
    }
}
