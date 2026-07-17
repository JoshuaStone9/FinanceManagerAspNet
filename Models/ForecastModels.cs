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
    public IReadOnlyList<ReserveAccountOption> Accounts { get; init; } = Array.Empty<ReserveAccountOption>();
    public IReadOnlyList<ReservePot> Pots { get; init; } = Array.Empty<ReservePot>();
}

public sealed class FinancialForecastResult
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal ProtectedReserveBaseline { get; init; }
    public decimal OpeningReserveBalance { get; init; }
    public decimal ProjectedReserveBalance { get; init; }
    public decimal ProjectedInterest { get; init; }
    public decimal ProjectedAccountContributions { get; init; }
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
    public decimal ProjectedBalance { get; init; }
    public DateTime? ProjectedCompletionDate { get; init; }
    public decimal? RequiredMonthlyContribution { get; init; }
    public decimal? AdditionalMonthlyContributionRequired { get; init; }
    public ForecastGoalStatus Status { get; init; }
    public string Explanation { get; init; } = string.Empty;
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
