namespace FinanceManagerAspNet.Models;

public sealed class MonthlyCashFlowForecastRequest
{
    public DateTime StartMonth { get; init; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public int Months { get; init; } = 12;
    public decimal MonthlyIncome { get; init; }
    public decimal OpeningCarryForward { get; init; }
    public decimal OpeningReserveBalance { get; init; }
    public decimal AnnualReserveInterestRate { get; init; }
    public IReadOnlyList<PaymentRow> Bills { get; init; } = Array.Empty<PaymentRow>();
    public IReadOnlyList<PaymentRow> EverydaySpending { get; init; } = Array.Empty<PaymentRow>();
    public IReadOnlyList<PaymentRow> Investments { get; init; } = Array.Empty<PaymentRow>();
    public IReadOnlyList<PaymentRow> ReserveAllocations { get; init; } = Array.Empty<PaymentRow>();
    public IReadOnlyList<PaymentRow> PlannedExpenses { get; init; } = Array.Empty<PaymentRow>();
    public IReadOnlyList<MonthlyForecastAdjustment> Adjustments { get; init; } = Array.Empty<MonthlyForecastAdjustment>();
}

public sealed record MonthlyForecastAdjustment(DateTime Month, decimal Amount, string Description);

public sealed class MonthlyCashFlowForecastRow
{
    public DateTime Month { get; init; }
    public decimal Income { get; init; }
    public decimal Bills { get; init; }
    public decimal EverydaySpending { get; init; }
    public decimal Investments { get; init; }
    public decimal HouseholdReserveContributions { get; init; }
    public decimal PlannedExpenses { get; init; }
    public decimal Adjustments { get; init; }
    public decimal InterestEarned { get; init; }
    public decimal OpeningCash { get; init; }
    public decimal RemainingCash { get; init; }
    public decimal ClosingReserveBalance { get; init; }
    public bool IsCurrentMonth { get; init; }
    public string MonthLabel => Month.ToString("MMMM yyyy");
    public bool HasShortfall => RemainingCash < 0m;
    public string StatusLabel => HasShortfall ? "Shortfall" : RemainingCash < 200m ? "Low buffer" : "Healthy";
    public string StatusCssClass => HasShortfall ? "bad" : RemainingCash < 200m ? "warn" : "good";
}

public sealed class MonthlyCashFlowForecastResult
{
    public IReadOnlyList<MonthlyCashFlowForecastRow> Months { get; init; } = Array.Empty<MonthlyCashFlowForecastRow>();
    public decimal TotalIncome => Months.Sum(x => x.Income);
    public decimal TotalOutgoings => Months.Sum(x => x.Bills + x.EverydaySpending + x.Investments + x.HouseholdReserveContributions + x.PlannedExpenses);
    public decimal TotalInterest => Months.Sum(x => x.InterestEarned);
    public decimal LowestRemainingCash => Months.Count == 0 ? 0m : Months.Min(x => x.RemainingCash);
    public int ShortfallMonthCount => Months.Count(x => x.HasShortfall);
}

public enum ForecastRecommendationType { CashShortfall, LowCashBuffer, GoalRisk, OverdrawnPot, CompletedGoal, ForecastSurplus }
public enum ForecastRecommendationSeverity { Information, Opportunity, Warning, Critical }

public sealed class ForecastRecommendation
{
    public ForecastRecommendationType Type { get; init; }
    public ForecastRecommendationSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal? SuggestedAmount { get; init; }
    public int? PotId { get; init; }
    public string? PotName { get; init; }
    public DateTime? Month { get; init; }
    public string SeverityCssClass => Severity switch
    {
        ForecastRecommendationSeverity.Critical => "bad",
        ForecastRecommendationSeverity.Warning => "warn",
        ForecastRecommendationSeverity.Opportunity => "good",
        _ => "muted"
    };
}

public sealed class CashFlowForecastPageViewModel
{
    public int Months { get; init; } = 12;
    public MonthlyCashFlowForecastResult Forecast { get; init; } = new();
    public IReadOnlyList<ForecastRecommendation> Recommendations { get; init; } = Array.Empty<ForecastRecommendation>();
}
