namespace FinanceManagerAspNet.Models;

public sealed class FinancialTrendsViewModel
{
    public int RangeMonths { get; init; }
    public DateTime RangeStart { get; init; }
    public DateTime RangeEnd { get; init; }
    public IReadOnlyList<FinancialTrendMonth> Months { get; init; } = [];

    public decimal AverageMonthlySurplus => Months.Count == 0 ? 0m : Months.Average(x => x.Remaining);
    public decimal TotalIncome => Months.Sum(x => x.TotalIncome);
    public decimal TotalOperatingIncome => Months.Sum(x => x.OperatingIncome);
    public decimal TotalPassiveIncome => Months.Sum(x => x.PassiveIncome);
    public decimal TotalSpending => Months.Sum(x => x.CoreSpending);
    public decimal TotalFutureAllocations => Months.Sum(x => x.FutureAllocations);
    public decimal PassiveIncomeShare => TotalIncome <= 0m ? 0m : TotalPassiveIncome / TotalIncome * 100m;
    public decimal BestMonthlySurplus => Months.Count == 0 ? 0m : Months.Max(x => x.Remaining);
    public FinancialTrendMonth? BestMonth => Months.OrderByDescending(x => x.Remaining).FirstOrDefault();
    public decimal CashFlowChartMaximum => Math.Max(1m, Months.SelectMany(x => new[] { x.TotalIncome, x.CoreSpending, x.FutureAllocations, Math.Abs(x.Remaining) }).DefaultIfEmpty(1m).Max());
    public decimal PassiveChartMaximum => Math.Max(1m, Months.Select(x => x.PassiveIncome).DefaultIfEmpty(1m).Max());
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
