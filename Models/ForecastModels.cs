namespace FinanceManagerAspNet.Models;

public enum ForecastGoalStatus
{
    Completed,
    OnTrack,
    AtRisk,
    Behind,
    NoContributionPlanned,
    NoTarget,
    NoDueDate,
    Overdrawn
}

public sealed class FinancialForecastRequest
{
    public DateTime StartDate { get; init; } = DateTime.Today;
    public DateTime EndDate { get; init; } = DateTime.Today.AddYears(1);
    public decimal ProtectedReserveBaseline { get; init; } = 12000m;
    public bool IncludeAccountContributions { get; init; } = true;
    public decimal FutureExpenseAmount { get; init; }
    public DateTime? FutureExpenseDate { get; init; }
    public IReadOnlyList<ReserveAccountOption> Accounts { get; init; } = Array.Empty<ReserveAccountOption>();
    public IReadOnlyList<ReservePot> Pots { get; init; } = Array.Empty<ReservePot>();
    public IReadOnlyDictionary<int, decimal> PotOneOffContributions { get; init; } = new Dictionary<int, decimal>();
    public IReadOnlyDictionary<int, decimal> PotMonthlyContributions { get; init; } = new Dictionary<int, decimal>();
    public IReadOnlyDictionary<int, List<ReservePotInvestmentStage>> InvestmentStages { get; init; } = new Dictionary<int, List<ReservePotInvestmentStage>>();
}

public sealed class FinancialForecastResult
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal ProtectedReserveBaseline { get; init; }
    public decimal OpeningReserveBalance { get; init; }
    public decimal ProjectedReserveBalance { get; init; }
    public decimal ProjectedInterest { get; init; }
    public decimal ProjectedGrossInterest { get; init; }
    public decimal ProjectedTax { get; init; }
    public decimal ProjectedAccountContributions { get; init; }
    public decimal ProjectedFutureExpenses { get; init; }
    public decimal ProjectedAllocatedToPots { get; init; }
    public decimal ProjectedSurplusAboveBaseline { get; init; }
    public decimal ProjectedUnallocatedSurplus { get; init; }
    public IReadOnlyList<MonthlyForecastRow> Months { get; init; } = Array.Empty<MonthlyForecastRow>();
    public IReadOnlyList<PotForecastResult> Pots { get; init; } = Array.Empty<PotForecastResult>();
    public IReadOnlyList<string> Assumptions { get; init; } = Array.Empty<string>();
    public int CompletedPotCount => Pots.Count(x => x.Status == ForecastGoalStatus.Completed);
    public int AtRiskPotCount => Pots.Count(x => x.Status is ForecastGoalStatus.AtRisk or ForecastGoalStatus.Behind or ForecastGoalStatus.Overdrawn);
}

public sealed record MonthlyForecastRow(
    DateTime Month,
    decimal OpeningReserveBalance,
    decimal Contributions,
    decimal Interest,
    decimal FutureExpenses,
    decimal ClosingReserveBalance,
    decimal ProtectedBaseline,
    decimal ProjectedAllocatedToPots,
    decimal AvailableSurplus)
{
    public string MonthLabel => Month.ToString("MMMM yyyy");
}

public sealed class PotForecastResult
{
    public int PotId { get; init; }
    public string PotName { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public decimal? TargetAmount { get; init; }
    public DateTime? DueDate { get; init; }
    public decimal IntendedMonthlyContribution { get; init; }
    public decimal RecentMonthlyContribution => IntendedMonthlyContribution;
    public decimal OneOffContribution { get; init; }
    public decimal ProjectedBalance { get; init; }
    public decimal ProjectedGrowth { get; init; }
    public string InvestmentJourneySummary { get; init; } = "No investment journey configured";
    public DateTime? ProjectedCompletionDate { get; init; }
    public decimal? RequiredMonthlyContribution { get; init; }
    public decimal? AdditionalMonthlyContributionRequired { get; init; }
    public int? MonthsEarlyOrLate { get; init; }
    public ForecastGoalStatus Status { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public string StatusLabel => Status switch
    {
        ForecastGoalStatus.OnTrack => "On track",
        ForecastGoalStatus.AtRisk => "At risk",
        ForecastGoalStatus.NoContributionPlanned => "No contribution planned",
        ForecastGoalStatus.NoTarget => "No target",
        ForecastGoalStatus.NoDueDate => "No due date",
        _ => Status.ToString()
    };
    public string StatusCssClass => Status switch
    {
        ForecastGoalStatus.Completed or ForecastGoalStatus.OnTrack => "good",
        ForecastGoalStatus.AtRisk => "warn",
        ForecastGoalStatus.Behind or ForecastGoalStatus.Overdrawn => "bad",
        _ => "muted"
    };
}

public sealed class ForecastPageViewModel
{
    public int Months { get; set; } = 12;
    public DateTime? CustomEndDate { get; set; }
    public bool IncludeAccountContributions { get; set; } = true;
    public FinancialForecastResult Forecast { get; set; } = new();
    public DateTime SelectedEndDate => CustomEndDate?.Date ?? DateTime.Today.AddMonths(Math.Max(1, Months));
}

public sealed class WhatIfForecastInput
{
    public int Months { get; set; } = 12;
    public int? PotId { get; set; }
    public decimal? MonthlyContributionOverride { get; set; }
    public decimal? TargetAmountOverride { get; set; }
    public DateTime? TargetDateOverride { get; set; }
    public decimal OneOffContribution { get; set; }
    public decimal? InterestRateOverride { get; set; }
    public decimal? ProtectedBaselineOverride { get; set; }
    public decimal FutureExpenseAmount { get; set; }
    public DateTime? FutureExpenseDate { get; set; }
}

public sealed class WhatIfForecastViewModel
{
    public WhatIfForecastInput Input { get; set; } = new();
    public IReadOnlyList<ReservePot> AvailablePots { get; set; } = Array.Empty<ReservePot>();
    public FinancialForecastResult CurrentPlan { get; set; } = new();
    public FinancialForecastResult ScenarioPlan { get; set; } = new();
    public PotForecastResult? CurrentPot { get; set; }
    public PotForecastResult? ScenarioPot { get; set; }
    public bool HasScenario { get; set; }
}


public sealed class SavedForecastScenario
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public int Months { get; set; } = 12;
    public int? PotId { get; set; }
    public decimal? MonthlyContributionOverride { get; set; }
    public decimal? TargetAmountOverride { get; set; }
    public DateTime? TargetDateOverride { get; set; }
    public decimal OneOffContribution { get; set; }
    public decimal? InterestRateOverride { get; set; }
    public decimal? ProtectedBaselineOverride { get; set; }
    public decimal FutureExpenseAmount { get; set; }
    public DateTime? FutureExpenseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public WhatIfForecastInput ToInput() => new()
    {
        Months = Months,
        PotId = PotId,
        MonthlyContributionOverride = MonthlyContributionOverride,
        TargetAmountOverride = TargetAmountOverride,
        TargetDateOverride = TargetDateOverride,
        OneOffContribution = OneOffContribution,
        InterestRateOverride = InterestRateOverride,
        ProtectedBaselineOverride = ProtectedBaselineOverride,
        FutureExpenseAmount = FutureExpenseAmount,
        FutureExpenseDate = FutureExpenseDate
    };
}

public sealed class ForecastScenarioListViewModel
{
    public IReadOnlyList<SavedForecastScenario> Scenarios { get; init; } = Array.Empty<SavedForecastScenario>();
    public IReadOnlyDictionary<int, string> PotNames { get; init; } = new Dictionary<int, string>();
}

public sealed class ForecastScenarioComparisonViewModel
{
    public SavedForecastScenario LeftScenario { get; init; } = new();
    public SavedForecastScenario RightScenario { get; init; } = new();
    public WhatIfForecastViewModel Left { get; init; } = new();
    public WhatIfForecastViewModel Right { get; init; } = new();
    public IReadOnlyList<SavedForecastScenario> AvailableScenarios { get; init; } = Array.Empty<SavedForecastScenario>();
}

public sealed class DashboardForecastSummary
{
    public decimal ProjectedReserveBalance { get; init; }
    public decimal ProjectedInterest { get; init; }
    public int GoalsAtRisk { get; init; }
    public int GoalsExpectedToComplete { get; init; }
    public string ScenarioLabel { get; init; } = "Live plan";
    public int? PreferredScenarioId { get; init; }
}
