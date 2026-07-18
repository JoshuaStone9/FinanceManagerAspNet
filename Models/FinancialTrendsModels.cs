namespace FinanceManagerAspNet.Models;

public sealed class FinancialTrendsViewModel
{
    public int RangeMonths { get; init; }
    public DateTime RangeStart { get; init; }
    public DateTime RangeEnd { get; init; }
    public IReadOnlyList<FinancialTrendMonth> Months { get; init; } = [];

    public IReadOnlyList<FinancialTrendMonth> ActiveMonths => Months.Where(x => x.HasData).ToList();
    public IReadOnlyList<FinancialTrendMonth> IncomeMonths => ActiveMonths.Where(x => x.TotalIncome > 0m).ToList();
    public int HiddenEmptyMonthCount => Months.Count - ActiveMonths.Count;
    public decimal AverageMonthlySurplus => IncomeMonths.Count == 0 ? 0m : IncomeMonths.Average(x => x.Remaining);
    public decimal TotalIncome => ActiveMonths.Sum(x => x.TotalIncome);
    public decimal TotalOperatingIncome => ActiveMonths.Sum(x => x.OperatingIncome);
    public decimal TotalPassiveIncome => ActiveMonths.Sum(x => x.PassiveIncome);
    public decimal TotalSpending => ActiveMonths.Sum(x => x.CoreSpending);
    public decimal TotalFutureAllocations => ActiveMonths.Sum(x => x.FutureAllocations);
    public decimal PassiveIncomeShare => TotalIncome <= 0m ? 0m : TotalPassiveIncome / TotalIncome * 100m;
    public decimal BestMonthlySurplus => ActiveMonths.Count == 0 ? 0m : ActiveMonths.Max(x => x.Remaining);
    public FinancialTrendMonth? BestMonth => ActiveMonths.OrderByDescending(x => x.Remaining).FirstOrDefault();
    public FinancialTrendMonth? HighestPassiveIncomeMonth => ActiveMonths.OrderByDescending(x => x.PassiveIncome).FirstOrDefault(x => x.PassiveIncome > 0m);
    public FinancialTrendMonth? HighestSpendingMonth => ActiveMonths.OrderByDescending(x => x.CoreSpending).FirstOrDefault(x => x.CoreSpending > 0m);
    public FinancialTrendMonth? LatestActiveMonth => ActiveMonths.OrderByDescending(x => x.MonthStart).FirstOrDefault();
    public FinancialTrendMonth? PreviousActiveMonth => ActiveMonths.OrderByDescending(x => x.MonthStart).Skip(1).FirstOrDefault();
    public decimal? LatestSurplusChange => LatestActiveMonth is null || PreviousActiveMonth is null ? null : LatestActiveMonth.Remaining - PreviousActiveMonth.Remaining;
    public decimal? LatestPassiveIncomeChange => LatestActiveMonth is null || PreviousActiveMonth is null ? null : LatestActiveMonth.PassiveIncome - PreviousActiveMonth.PassiveIncome;
    public decimal? LatestSpendingChange => LatestActiveMonth is null || PreviousActiveMonth is null ? null : LatestActiveMonth.CoreSpending - PreviousActiveMonth.CoreSpending;
    public decimal CashFlowChartMaximum => Math.Max(1m, ActiveMonths.SelectMany(x => new[] { x.TotalIncome, x.CoreSpending, x.FutureAllocations, Math.Abs(x.Remaining) }).DefaultIfEmpty(1m).Max());
    public decimal PassiveChartMaximum => Math.Max(1m, ActiveMonths.Select(x => x.PassiveIncome).DefaultIfEmpty(1m).Max());

    public IReadOnlyList<string> Insights
    {
        get
        {
            var insights = new List<string>();

            if (BestMonth is not null)
            {
                insights.Add($"{BestMonth.FullLabel} produced the strongest monthly result at {BestMonth.Remaining:C}.");
            }

            if (HighestPassiveIncomeMonth is not null)
            {
                insights.Add($"Passive income peaked in {HighestPassiveIncomeMonth.FullLabel} at {HighestPassiveIncomeMonth.PassiveIncome:C}.");
            }

            if (LatestSurplusChange.HasValue && LatestActiveMonth is not null && PreviousActiveMonth is not null)
            {
                var direction = LatestSurplusChange.Value >= 0m ? "improved" : "fell";
                insights.Add($"Remaining money {direction} by {Math.Abs(LatestSurplusChange.Value):C} from {PreviousActiveMonth.FullLabel} to {LatestActiveMonth.FullLabel}.");
            }

            if (HighestSpendingMonth is not null)
            {
                insights.Add($"Core spending was highest in {HighestSpendingMonth.FullLabel} at {HighestSpendingMonth.CoreSpending:C}.");
            }

            return insights;
        }
    }
}

public sealed class FinancialTrendMonth
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal CarryForward { get; init; }
    public decimal TotalIncome { get; init; }
    public decimal OperatingIncome { get; init; }
    public decimal PassiveIncome { get; init; }
    public decimal InterestIncome { get; init; }
    public decimal DividendIncome { get; init; }
    public decimal RentalIncome { get; init; }
    public decimal OtherPassiveIncome { get; init; }
    public decimal EssentialBills { get; init; }
    public decimal EverydaySpending { get; init; }
    public decimal ExtraExpenses { get; init; }
    public decimal Investments { get; init; }
    public decimal MoneyPots { get; init; }

    public DateTime MonthStart => new(Year, Month, 1);
    public string Label => MonthStart.ToString("MMM yy");
    public string FullLabel => MonthStart.ToString("MMMM yyyy");
    public decimal AvailableIncome => TotalIncome + CarryForward;
    public decimal CoreSpending => EssentialBills + EverydaySpending + ExtraExpenses;
    public decimal FutureAllocations => Investments + MoneyPots;
    public decimal Remaining => AvailableIncome - CoreSpending - FutureAllocations;
    public bool HasData => TotalIncome != 0m || CarryForward != 0m || CoreSpending != 0m || FutureAllocations != 0m;
}
