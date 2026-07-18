using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IFinancialTrendsService
{
    Task<FinancialTrendsViewModel> BuildAsync(int rangeMonths, DateTime? endMonth = null);
}

public sealed class FinancialTrendsService(FinanceRepository repo) : IFinancialTrendsService
{
    private static readonly HashSet<string> PassiveCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Interest", "Dividend", "Dividends", "Rental income", "Rental", "Other passive income", "Passive income"
    };

    public async Task<FinancialTrendsViewModel> BuildAsync(int rangeMonths, DateTime? endMonth = null)
    {
        rangeMonths = rangeMonths is 3 or 6 or 12 ? rangeMonths : 6;
        var end = new DateTime((endMonth ?? DateTime.Today).Year, (endMonth ?? DateTime.Today).Month, 1);
        var start = end.AddMonths(-(rangeMonths - 1));
        var months = new List<FinancialTrendMonth>(rangeMonths);

        for (var cursor = start; cursor <= end; cursor = cursor.AddMonths(1))
        {
            var incomeEntries = await repo.GetMonthlyIncomeEntriesAsync(cursor.Year, cursor.Month);
            var totalIncome = incomeEntries.Sum(x => x.Amount);
            var passiveEntries = incomeEntries.Where(IsPassiveIncome).ToList();
            var passiveIncome = passiveEntries.Sum(x => x.Amount);
            var carryForward = (await repo.GetCarryForwardInfoAsync(cursor.Year, cursor.Month)).EffectiveAmount;
            var bills = await repo.GetRowsAsync("bills", cursor.Month, cursor.Year);
            var everyday = await repo.GetRowsAsync("everyday_spending", cursor.Month, cursor.Year);
            var extras = await repo.GetRowsAsync("extra_expenses", cursor.Month, cursor.Year);
            var investments = await repo.GetRowsAsync("investments", cursor.Month, cursor.Year);
            var moneyPots = await repo.GetRowsAsync("savings", cursor.Month, cursor.Year);

            months.Add(new FinancialTrendMonth
            {
                Year = cursor.Year,
                Month = cursor.Month,
                CarryForward = carryForward,
                TotalIncome = totalIncome,
                OperatingIncome = Math.Max(0m, totalIncome - passiveIncome),
                PassiveIncome = passiveIncome,
                InterestIncome = passiveEntries.Where(x => CategoryIs(x.Category, "Interest")).Sum(x => x.Amount),
                DividendIncome = passiveEntries.Where(x => CategoryIs(x.Category, "Dividend") || CategoryIs(x.Category, "Dividends")).Sum(x => x.Amount),
                RentalIncome = passiveEntries.Where(x => CategoryIs(x.Category, "Rental") || CategoryIs(x.Category, "Rental income")).Sum(x => x.Amount),
                OtherPassiveIncome = passiveEntries.Where(x => !CategoryIs(x.Category, "Interest") && !CategoryIs(x.Category, "Dividend") && !CategoryIs(x.Category, "Dividends") && !CategoryIs(x.Category, "Rental") && !CategoryIs(x.Category, "Rental income")).Sum(x => x.Amount),
                EssentialBills = bills.Sum(x => x.Amount),
                EverydaySpending = everyday.Sum(x => x.Amount),
                ExtraExpenses = extras.Sum(x => x.Amount),
                Investments = investments.Sum(x => x.Amount),
                MoneyPots = moneyPots.Sum(x => x.Amount)
            });
        }

        return new FinancialTrendsViewModel
        {
            RangeMonths = rangeMonths,
            RangeStart = start,
            RangeEnd = end,
            Months = months
        };
    }

    private static bool IsPassiveIncome(MonthlyIncomeEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Category) && PassiveCategories.Contains(entry.Category.Trim());

    private static bool CategoryIs(string? category, string expected) =>
        string.Equals(category?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}
