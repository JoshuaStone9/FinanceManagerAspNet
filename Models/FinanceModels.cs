namespace FinanceManagerAspNet.Models;

public sealed record AccountBalance(
    int Id,
    string Name,
    decimal Amount,
    decimal InterestRate,
    decimal MonthlyContribution,
    bool IncludeInGlobalGoal,
    DateTime UpdatedAt,
    bool IncludeInSavingsCommand = false,
    decimal StartingBalance = 0m,
    string Provider = "Other",
    string AccountType = "Savings",
    string HoldingType = "Cash");
public sealed record LastModifiedInfo(string KeyName, DateTime? UpdatedAt);
public sealed record IncomeSnapshot(int Year, int Month, decimal Amount, int SickDays, DateTime UpdatedAt);

public sealed record InterestProjection(
    string Name,
    decimal CurrentBalance,
    decimal AnnualInterestRate,
    decimal MonthlyContribution,
    int Months,
    decimal ContributionsAdded,
    decimal InterestEarned,
    decimal ProjectedBalance,
    DateTime UpdatedAt);

public sealed class DashboardViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime MonthStart => new(Year, Month, 1);
    public DateTime PreviousMonth => MonthStart.AddMonths(-1);
    public DateTime NextMonth => MonthStart.AddMonths(1);
    // Legacy forecast fields are retained for compatibility with the Statistics page,
    // but are no longer used by the post-move monthly dashboard.
    public decimal MonthlySavingTarget { get; set; }
    public decimal GlobalGoal { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal OperatingIncome { get; set; }
    public decimal PassiveIncome { get; set; }
    public decimal CarryForwardAmount { get; set; }
    public decimal CarryForwardCalculated { get; set; }
    public decimal? CarryForwardOverride { get; set; }
    public string? CarryForwardOverrideReason { get; set; }
    public bool HasCarryForwardOverride => CarryForwardOverride.HasValue;
    public decimal EffectiveIncome => MonthlyIncome + CarryForwardAmount;
    public int SickDays { get; set; }
    public decimal BillsTotal { get; set; }
    public decimal ExpensesTotal { get; set; }
    public decimal ExtraExpensesTotal { get; set; }
    public decimal InvestmentsTotal { get; set; }
    public decimal SavingsTotal { get; set; }
    public decimal TotalAllocated => BillsTotal + ExpensesTotal + ExtraExpensesTotal + InvestmentsTotal + SavingsTotal;
    public decimal RemainingFund => EffectiveIncome - TotalAllocated;
    public bool IsOverBudget => RemainingFund < 0;
    public decimal OverspendAmount => Math.Max(0, -RemainingFund);
    public decimal TotalGoalBalance { get; set; }
    public decimal VaultTotalValue { get; set; }
    public int VaultItemCount { get; set; }
    public decimal StocksCryptoValue { get; set; }
    public decimal LiveAssetsValue { get; set; }
    public decimal LiveAssetsMonthlyContribution { get; set; }
    public decimal LiveAssetsGrowthRate { get; set; }
    public DateTime? LiveAssetsLastUpdated { get; set; }
    public decimal StocksCryptoInterestRate { get; set; }
    public decimal StocksCryptoMonthlyContribution { get; set; }
    public decimal ProjectedStocksCryptoByGoalDate { get; set; }
    public decimal TotalValueNow => TotalGoalBalance + VaultTotalValue + LiveAssetsValue;
    public decimal TotalValueByGoalDate => ProjectedWithInterestByGoalDate + VaultTotalValue + ProjectedStocksCryptoByGoalDate;
    public decimal GlobalGoalRemaining => Math.Max(0, GlobalGoal - TotalGoalBalance);
    public decimal ProjectedWithoutInterestByGoalDate { get; set; }
    public decimal ProjectedWithInterestByGoalDate { get; set; }
    public decimal ProjectedInterestByGoalDate { get; set; }
    public decimal ProjectedSalarySavingsByGoalDate { get; set; }
    public DateTime TargetDate { get; set; }
    public int MonthsToGoalAtCurrentPace { get; set; }
    public List<PaymentRow> Bills { get; set; } = [];
    public List<PaymentRow> Expenses { get; set; } = [];
    public List<PaymentRow> ExtraExpenses { get; set; } = [];
    public List<PaymentRow> Investments { get; set; } = [];
    public List<PaymentRow> Savings { get; set; } = [];
    public List<AccountBalance> Accounts { get; set; } = [];
    public List<LastModifiedInfo> LastModified { get; set; } = [];
    public DashboardIntelligenceSummary Intelligence { get; set; } = new();
    public DashboardForecastSummary ForecastSummary { get; set; } = new();
    public DashboardExperience Experience { get; set; } = new();
    public MonthlyFinancialHealthSummary FinancialHealth { get; set; } = new();
}

public sealed class DashboardIntelligenceSummary
{
    public int ActivePotCount { get; set; }
    public decimal MonthlyPlanned { get; set; }
    public decimal FundedThisMonth { get; set; }
    public decimal RemainingThisMonth { get; set; }
    public int DueReminderCount { get; set; }
    public int OverdueOrMissedCount { get; set; }
    public int PartiallyFundedCount { get; set; }
    public int PausedCount { get; set; }
    public int CompletedCount { get; set; }
    public int HealthyCount { get; set; }
    public int ApproachingTargetCount { get; set; }
    public decimal AvailableReserve { get; set; }
    public decimal RecommendedAllocationTotal { get; set; }
    public decimal RemainingReserveAfterRecommendations => Math.Max(0m, AvailableReserve - RecommendedAllocationTotal);
    public List<ReserveRecoveryRecommendation> Recommendations { get; set; } = [];
    public List<FinanceReminderRow> DueReminders { get; set; } = [];
    public List<DashboardUpcomingTarget> UpcomingTargets { get; set; } = [];
}

public sealed record DashboardUpcomingTarget(
    int PotId,
    string PotName,
    DateTime DueDate,
    decimal RemainingAmount)
{
    public int DaysRemaining => (DueDate.Date - DateTime.Today).Days;
    public string UrgencyCssClass => DaysRemaining <= 30 ? "bad" : DaysRemaining <= 60 ? "warn" : "good";
}

public sealed class HouseGoalViewModel
{
    public decimal ExternalTotalValue { get; set; }
    public decimal HouseGoal { get; set; } = 30000m;
    public decimal MoneyboxFundAmount { get; set; }
    public decimal MoneyboxInterestRate { get; set; } = 3.8m;
    public decimal MoneyboxBonus { get; set; }
    public int ForecastMonths { get; set; }
    public decimal MonthlyHouseSaving { get; set; }
    public decimal PlannedHouseSavings { get; set; }
    public decimal MoneyboxProjectedBalance { get; set; }
    public decimal MoneyboxInterestOnly { get; set; }
    public decimal HouseNormalTotal { get; set; }
    public decimal HouseWithInterestTotal { get; set; }
    public decimal HouseRemainingNormal { get; set; }
    public decimal HouseRemainingWithInterest { get; set; }
    public decimal EmergencyFundStillNeededNormal { get; set; }
    public decimal EmergencyFundStillNeededWithInterest { get; set; }
    public decimal StandaloneInterestAmount { get; set; }
    public decimal StandaloneInterestRate { get; set; } = 3.8m;
    public decimal StandaloneMonthlyContribution { get; set; }
    public int StandaloneMonths { get; set; }
    public decimal StandaloneInterestMade { get; set; }
    public decimal StandaloneProjectedTotal { get; set; }
}



public sealed class AssetHolding
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "Manual";
    public string? Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal? AverageBuyPrice { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public string Currency { get; set; } = "GBP";
    public bool UseLivePrice { get; set; }

    // Broker is where you hold it, e.g. Trading 212, Moneybox, Coinbase.
    public string? Broker { get; set; }

    // PriceSource is where the app fetches prices from. Use Auto for most holdings.
    public string PriceSource { get; set; } = "Auto";

    // Provider is kept for older forms/database rows. Treat it as broker fallback.
    public string? Provider { get; set; }

    // Bullion-specific fields. For gold/silver coins and bars, Quantity is the number of items,
    // MetalWeightOz is the troy-ounce weight per item, and PremiumValue is any extra coin/bar premium.
    public decimal? MetalWeightOz { get; set; }
    public decimal? MetalPurity { get; set; }
    public decimal? PremiumValue { get; set; }
    public int? MetalYear { get; set; }
    public string? BullionSeries { get; set; }
    public string? BullionForm { get; set; }

    // SpotPremium = live metal spot value + premium. Manual = collectible/proof value entered by you.
    public string ValuationMethod { get; set; } = "SpotPremium";

    public decimal? ManualValue { get; set; }
    public decimal? AnnualGrowthRate { get; set; }
    public decimal? MonthlyContribution { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? LastPriceUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsBullion => AssetType.Equals("Gold", StringComparison.OrdinalIgnoreCase) || AssetType.Equals("Silver", StringComparison.OrdinalIgnoreCase);
    public decimal EffectiveMetalWeightOz => MetalWeightOz ?? 0m;
    public string BullionDescription => IsBullion
        ? string.Join(" ", new[] { BrokerDisplay, BullionSeries, MetalYear?.ToString(), BullionForm }.Where(x => !string.IsNullOrWhiteSpace(x)))
        : string.Empty;
    public bool UsesSpotValuation => IsBullion && (string.IsNullOrWhiteSpace(ValuationMethod) || ValuationMethod.Equals("SpotPremium", StringComparison.OrdinalIgnoreCase) || ValuationMethod.Equals("Spot", StringComparison.OrdinalIgnoreCase));
    public bool UsesManualValuation => IsBullion && !UsesSpotValuation;
    public decimal DisplayValue => CurrentValue ?? ManualValue ?? 0m;
    public decimal CostBasis => Math.Round(Quantity * (AverageBuyPrice ?? 0m), 2);
    public decimal ProfitLoss => AverageBuyPrice.HasValue ? Math.Round(DisplayValue - CostBasis, 2) : 0m;
    public decimal? ProfitLossPercent => CostBasis > 0 ? Math.Round((ProfitLoss / CostBasis) * 100m, 2) : null;
    public int? DaysHeld => PurchaseDate.HasValue ? Math.Max(0, (DateTime.Today - PurchaseDate.Value.Date).Days) : null;
    public string BrokerDisplay => !string.IsNullOrWhiteSpace(Broker) ? Broker! : Provider ?? "Manual";
    public string PriceSourceDisplay => string.IsNullOrWhiteSpace(PriceSource) ? "Auto" : PriceSource;
}

public sealed record LivePriceUpdateResult(int UpdatedCount, List<string> UpdatedNames, List<string> FailedNames);

public sealed record AssetSummary(decimal TotalValue, decimal MonthlyContribution, decimal WeightedGrowthRate, DateTime? LastUpdated);

public sealed class AssetsViewModel
{
    public List<AssetHolding> Assets { get; set; } = [];
    public AssetSummary Summary { get; set; } = new(0, 0, 0, null);
    public decimal VaultTotalValue { get; set; }
    public decimal GoalAccountsTotal { get; set; }
    public decimal TotalNetAssets => Summary.TotalValue + VaultTotalValue + GoalAccountsTotal;
}

public sealed record SavingPot(
    int Id,
    string Name,
    decimal TargetAmount,
    decimal MonthlyAmount,
    string ContributionMode,
    int Priority,
    string PotType,
    string AccessSpeed,
    decimal InterestRate,
    DateTime? TargetDate,
    string? Destination,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public bool UsesGlobalContribution => ContributionMode.Equals("Uses global savings schedule", StringComparison.OrdinalIgnoreCase);
    public bool UsesManualContribution => ContributionMode.Equals("Manual contribution", StringComparison.OrdinalIgnoreCase);
    public bool IsOneOffFundingOnly => ContributionMode.Equals("One-off funding only", StringComparison.OrdinalIgnoreCase);
    public bool IsPaused => ContributionMode.Equals("Paused", StringComparison.OrdinalIgnoreCase);
}

public sealed record SavingPotMonth(
    int Id,
    int SavingPotId,
    int Year,
    int Month,
    bool IsSaved,
    decimal SavedAmount,
    DateTime UpdatedAt);

public sealed class SavingPotRowViewModel
{
    public SavingPot Pot { get; set; } = new(0, string.Empty, 0, 0, "Uses global savings schedule", 1, "Goal", "Immediate", 0, null, null, DateTime.MinValue, DateTime.MinValue);
    public List<SavingPotMonth> Months { get; set; } = [];
    public decimal ManualSavedBalance { get; set; }
    public decimal AutoAllocatedBalance { get; set; }

    // Amount actually available to this pot right now. This is 0 until the emergency baseline is complete.
    public decimal ForecastMonthlyAmount { get; set; }

    // Amount expected to flow to this pot after the emergency baseline is complete.
    public decimal ForecastMonthlyAfterBaseline { get; set; }
    public bool EmergencyBaselineComplete { get; set; }
    public int? EmergencyBaselineMonthsRemaining { get; set; }
    public int FundingQueuePosition { get; set; }

    public decimal SavedBalance => ManualSavedBalance + AutoAllocatedBalance;
    public decimal Remaining => Math.Max(0, Pot.TargetAmount - SavedBalance);
    public decimal CompletionPercent => Pot.TargetAmount <= 0 ? 100m : Math.Min(100m, Math.Round((SavedBalance / Pot.TargetAmount) * 100m, 1));
    public bool IsComplete => Pot.TargetAmount > 0 && SavedBalance >= Pot.TargetAmount;
    public bool WaitingForEmergencyBaseline => !EmergencyBaselineComplete && !IsComplete;
    public decimal MonthlyInterestEstimate => Math.Round(SavedBalance * (Pot.InterestRate / 100m) / 12m, 2);
    public decimal AnnualInterestEstimate => Math.Round(SavedBalance * (Pot.InterestRate / 100m), 2);

    public int? MonthsToTarget
    {
        get
        {
            if (IsComplete) return 0;
            if (ForecastMonthlyAfterBaseline <= 0) return null;
            var potMonths = (int)Math.Ceiling(Remaining / ForecastMonthlyAfterBaseline);
            return (EmergencyBaselineMonthsRemaining ?? 0) + potMonths;
        }
    }

    public DateTime? EstimatedFinishDate => MonthsToTarget.HasValue ? DateTime.Today.AddMonths(MonthsToTarget.Value) : null;

    public int? MonthsUntilTargetDate
    {
        get
        {
            if (!Pot.TargetDate.HasValue) return null;
            var target = new DateTime(Pot.TargetDate.Value.Year, Pot.TargetDate.Value.Month, 1);
            var today = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return Math.Max(0, ((target.Year - today.Year) * 12) + target.Month - today.Month);
        }
    }

    public decimal? RequiredMonthlyToTargetDate
    {
        get
        {
            if (!MonthsUntilTargetDate.HasValue || MonthsUntilTargetDate.Value <= 0) return null;
            var usableMonths = Math.Max(0, MonthsUntilTargetDate.Value - (EmergencyBaselineMonthsRemaining ?? 0));
            if (usableMonths <= 0) return null;
            return Math.Round(Remaining / usableMonths, 2);
        }
    }

    public string EstimatedFinishLabel
    {
        get
        {
            if (IsComplete) return "Complete";
            if (WaitingForEmergencyBaseline && !MonthsToTarget.HasValue) return "Waiting for emergency fund";
            return EstimatedFinishDate?.ToString("MMM yyyy") ?? "No forecast";
        }
    }

    public string TargetStatus
    {
        get
        {
            if (IsComplete) return "Complete";
            if (!Pot.TargetDate.HasValue) return WaitingForEmergencyBaseline ? "Waiting for emergency fund" : "No target date";
            if (!EstimatedFinishDate.HasValue) return WaitingForEmergencyBaseline ? "Waiting for emergency fund" : "No forecast";
            var target = Pot.TargetDate.Value.Date;
            if (EstimatedFinishDate.Value.Date <= target) return "On track";
            return "Behind target";
        }
    }
}

public sealed record SavingsContributionChange(
    int Id,
    DateTime StartsOn,
    decimal MonthlyAmount,
    string? Note,
    DateTime CreatedAt);

public sealed record ReservedFund(
    int Id,
    string Name,
    decimal Amount,
    string Category,
    string AccessSpeed,
    bool IncludeInNetWorth,
    bool DeductFromSavingsAllocation,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);


public sealed class SavingPotForecastAtDate
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? TargetDate { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal ProjectedAllocated { get; set; }
    public decimal Remaining => Math.Max(0, TargetAmount - ProjectedAllocated);
    public decimal CompletionPercent => TargetAmount <= 0 ? 100m : Math.Min(100m, Math.Round((ProjectedAllocated / TargetAmount) * 100m, 1));
    public bool IsComplete => TargetAmount > 0 && ProjectedAllocated >= TargetAmount;
}

public sealed class SavingsRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public int Stars { get; set; } = 3;
    public string Tone { get; set; } = "info";
}

public sealed class SavingPotsViewModel
{
    public int Year { get; set; }
    public decimal EmergencyFundTotal { get; set; }
    public decimal GrossSelectedFundTotal { get; set; }
    public decimal ReservedFundsTotal => ReservedFunds.Where(r => r.DeductFromSavingsAllocation).Sum(r => r.Amount);
    public List<ReservedFund> ReservedFunds { get; set; } = [];
    public decimal EmergencyBaseline { get; set; } = 12000m;
    public decimal AvailableOverflow { get; set; }
    public decimal AllocatedToPots { get; set; }
    public decimal UnallocatedOverflow => Math.Max(0, AvailableOverflow - AllocatedToPots);
    public decimal EmergencyFundHeld => Math.Min(EmergencyFundTotal, EmergencyBaseline);
    public decimal EmergencyFundShortfall => Math.Max(0, EmergencyBaseline - EmergencyFundTotal);
    public decimal EmergencyFundProgress => EmergencyBaseline <= 0 ? 100m : Math.Min(100m, Math.Round((EmergencyFundHeld / EmergencyBaseline) * 100m, 1));
    public decimal InterestRate { get; set; }
    public decimal MonthlySavingRate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateTime OverallTargetDate { get; set; } = new(DateTime.Today.Year, 4, 1);
    public decimal EmergencyMonthlyInterest => Math.Round(EmergencyFundHeld * (InterestRate / 100m) / 12m, 2);
    public decimal EmergencyAnnualInterest => Math.Round(EmergencyFundHeld * (InterestRate / 100m), 2);
    public decimal CurrentFundMonthlyInterest => Math.Round(EmergencyFundTotal * (InterestRate / 100m) / 12m, 2);
    public decimal CurrentFundAnnualInterest => Math.Round(EmergencyFundTotal * (InterestRate / 100m), 2);
    public int? MonthsToOverallTarget => OverallTargetDate <= DateTime.Today ? 0 : ((OverallTargetDate.Year - DateTime.Today.Year) * 12) + OverallTargetDate.Month - DateTime.Today.Month;
    public decimal ProjectedEmergencyByTarget { get; set; }
    public decimal TotalPotTargets => Pots.Sum(p => p.Pot.TargetAmount);
    public decimal TotalPotProgress => Pots.Sum(p => p.SavedBalance);
    public decimal TotalMonthlyPotInterest => Pots.Sum(p => p.MonthlyInterestEstimate);
    public List<AccountBalance> SavingsSources { get; set; } = [];
    public List<SavingsContributionChange> ContributionSchedule { get; set; } = [];
    public DateTime ForecastDate { get; set; } = new(DateTime.Today.Year, 4, 1);
    public int ForecastMonths => ForecastDate <= DateTime.Today ? 0 : ((ForecastDate.Year - DateTime.Today.Year) * 12) + ForecastDate.Month - DateTime.Today.Month;
    public decimal ForecastProjectedFundTotal { get; set; }
    public decimal ForecastProjectedEmergencyHeld => Math.Min(ForecastProjectedFundTotal, EmergencyBaseline);
    public decimal ForecastProjectedOverflow => Math.Max(0, ForecastProjectedFundTotal - EmergencyBaseline);
    public decimal ForecastProjectedPotProgress { get; set; }
    public decimal ForecastProjectedUnallocatedOverflow => Math.Max(0, ForecastProjectedOverflow - ForecastProjectedPotProgress);
    public List<SavingPotForecastAtDate> ForecastPots { get; set; } = [];
    public decimal TargetTotalNeeded => EmergencyBaseline + TotalPotTargets;
    public decimal CurrentTotalShortfall => Math.Max(0, TargetTotalNeeded - EmergencyFundTotal);
    public decimal ForecastTotalShortfall => Math.Max(0, TargetTotalNeeded - ForecastProjectedFundTotal);
    public decimal? RequiredMonthlyToFullyFundByForecast => ForecastMonths <= 0 ? null : Math.Round(CurrentTotalShortfall / ForecastMonths, 2);
    public int BehindPotCount => Pots.Count(p => p.TargetStatus == "Behind target");
    public List<SavingPotRowViewModel> Pots { get; set; } = [];
    public List<SavingsRecommendation> Recommendations { get; set; } = [];
}

public sealed record StatisticsForecastMonth(
    int Year,
    int Month,
    decimal Result,
    decimal InterestReceived)
{
    public DateTime MonthDate => new(Year, Month, 1);
}

public sealed class StatisticsViewModel
{
    public decimal GlobalGoal { get; set; }
    public decimal MonthlyForecastContribution { get; set; }
    public string ForecastMethod { get; set; } = "Last3Months";
    public string ForecastMethodLabel { get; set; } = "Last 3 completed months";
    public string ForecastConfidence { get; set; } = "Limited history";
    public string ForecastConfidenceCssClass { get; set; } = "warning";
    public int ForecastHistoryCount { get; set; }
    public List<StatisticsForecastMonth> ForecastHistory { get; set; } = [];
    public decimal AccountMonthlyContributions { get; set; }
    public decimal ForecastContributionsByGoalDate { get; set; }
    public decimal AccountContributionsByGoalDate { get; set; }
    public decimal ForecastContributionInterest { get; set; }
    public decimal ForecastWeightedInterestRate { get; set; }
    public decimal InterestReceivedInForecastHistory { get; set; }
    public decimal InterestReceivedThisYear { get; set; }
    public decimal ExpectedInterestThisMonth { get; set; }
    public decimal InterestReceivedThisMonth { get; set; }
    public decimal ProjectedFutureInterest { get; set; }
    public decimal ManualAverageIncome { get; set; }
    public decimal CalculatedSalaryEstimate { get; set; }
    public decimal AverageSavingPace { get; set; }
    public decimal TotalNow { get; set; }
    public decimal Remaining { get; set; }
    public DateTime AprilTarget { get; set; }
    public int MonthsToApril { get; set; }
    public decimal ProjectedSalarySavingsByApril { get; set; }
    public decimal ProjectedByAprilWithoutInterest { get; set; }
    public decimal ProjectedByAprilWithInterest { get; set; }
    public decimal ProjectedInterestEarned { get; set; }
    public decimal RemainingByAprilWithoutInterest => Math.Max(0, GlobalGoal - ProjectedByAprilWithoutInterest);
    public decimal RemainingByAprilWithInterest => Math.Max(0, GlobalGoal - ProjectedByAprilWithInterest);
    public int EstimatedMonthsToGoal { get; set; }
    public DateTime? EstimatedGoalDate { get; set; }
    public string PatternMessage { get; set; } = string.Empty;
    public List<AccountBalance> Accounts { get; set; } = [];
    public List<InterestProjection> InterestProjections { get; set; } = [];
    public List<IncomeSnapshot> IncomeHistory { get; set; } = [];
    public List<LastModifiedInfo> UpdatePattern { get; set; } = [];
    public HouseGoalViewModel HouseGoal { get; set; } = new();
    public decimal AllocatedToSavingPots { get; set; }
    public decimal VaultTotalValue { get; set; }
    public int VaultItemCount { get; set; }
    public decimal StocksCryptoValue { get; set; }
    public decimal LiveAssetsValue { get; set; }
    public decimal LiveAssetsMonthlyContribution { get; set; }
    public decimal LiveAssetsGrowthRate { get; set; }
    public DateTime? LiveAssetsLastUpdated { get; set; }
    public decimal StocksCryptoInterestRate { get; set; }
    public decimal StocksCryptoMonthlyContribution { get; set; }
    public decimal ProjectedStocksCryptoByApril { get; set; }
    public decimal TotalValueNow { get; set; }
    public decimal TotalValueByApril { get; set; }
    public decimal AvailableEmergencyFundAfterPots => Math.Round(HouseGoal.EmergencyFundStillNeededWithInterest - AllocatedToSavingPots, 2);
}

public sealed record ReserveAccountOption(
    int Id,
    string Name,
    decimal Balance,
    decimal InterestRate,
    decimal MonthlyContribution,
    bool IsSelected);


public sealed record ReserveAccountInterestForecast(
    int AccountId,
    string AccountName,
    decimal OpeningBalance,
    decimal AnnualInterestRate,
    decimal MonthlyContribution,
    decimal ContributionsAdded,
    decimal EstimatedInterest,
    decimal ProjectedBalance);

public sealed class ReserveInterestForecast
{
    public DateTime StartDate { get; init; } = DateTime.Today;
    public DateTime ForecastDate { get; init; } = DateTime.Today.AddYears(1);
    public bool IncludeFutureContributions { get; init; }
    public IReadOnlyList<ReserveAccountInterestForecast> Accounts { get; init; } = Array.Empty<ReserveAccountInterestForecast>();
    public int ForecastDays => Math.Max(0, (ForecastDate.Date - StartDate.Date).Days);
    public decimal CurrentTotal => Accounts.Sum(x => x.OpeningBalance);
    public decimal FutureContributions => Accounts.Sum(x => x.ContributionsAdded);
    public decimal EstimatedInterest => Accounts.Sum(x => x.EstimatedInterest);
    public decimal ProjectedTotal => Accounts.Sum(x => x.ProjectedBalance);
}

public sealed class HouseholdReserveAccountSummary
{
    public decimal Baseline { get; init; } = 12000m;
    public IReadOnlyList<ReserveAccountOption> AvailableAccounts { get; init; } = Array.Empty<ReserveAccountOption>();
    public IReadOnlyList<ReserveAccountOption> SelectedAccounts { get; init; } = Array.Empty<ReserveAccountOption>();
    public decimal TotalBalance => SelectedAccounts.Sum(x => x.Balance);
    public decimal BaselineCovered => Math.Min(TotalBalance, Baseline);
    public decimal BaselineShortfall => Math.Max(0m, Baseline - TotalBalance);
    public decimal SurplusAboveBaseline => Math.Max(0m, TotalBalance - Baseline);
    public bool HasSelection => SelectedAccounts.Count > 0;
}

public sealed record HouseholdReserve(
    decimal Balance,
    decimal InterestRate,
    string Provider,
    DateTime UpdatedAt);

public sealed record ReservePotPickerItem(
    int Id,
    string Name,
    decimal AllocatedAmount,
    decimal? TargetAmount,
    bool IsActive);

public sealed record ReservePot(
    int Id,
    string Name,
    decimal AllocatedAmount,
    decimal DefaultMonthlyContribution,
    decimal IntendedMonthlyContribution,
    string FundingFrequency,
    int? ExpectedFundingDay,
    bool CarryForwardShortfalls,
    bool CarryExcessForward,
    DateTime? FundingPausedFrom,
    DateTime? FundingPausedUntil,
    string? FundingPauseReason,
    DateTime FundingPlanStartDate,
    decimal? TargetAmount,
    DateTime? DueDate,
    int Priority,
    bool IsActive,
    string? Notes,
    decimal StartingAmount,
    DateTime UpdatedAt)
{
    public bool IsFundingPaused => IsPausedFor(DateTime.Today);
    public bool IsPausedFor(DateTime date)
    {
        if (!FundingPausedUntil.HasValue) return false;
        var from = (FundingPausedFrom ?? DateTime.Today).Date;
        return date.Date >= from && date.Date <= FundingPausedUntil.Value.Date;
    }
    public string FundingPlanLabel => $"{IntendedMonthlyContribution:C} flexible monthly target";

    public bool IsOverdrawn => AllocatedAmount < 0m;
    public decimal NegativeBalanceRecoveryRequired => Math.Max(0m, -AllocatedAmount);
    public decimal RemainingToTarget => TargetAmount.HasValue ? Math.Max(0, TargetAmount.Value - AllocatedAmount) : 0m;

    public int? MonthsUntilDue
    {
        get
        {
            if (!DueDate.HasValue) return null;
            var firstOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var firstOfDueMonth = new DateTime(DueDate.Value.Year, DueDate.Value.Month, 1);
            return Math.Max(1, ((firstOfDueMonth.Year - firstOfThisMonth.Year) * 12) + firstOfDueMonth.Month - firstOfThisMonth.Month);
        }
    }

    public decimal? SuggestedMonthlyContribution
    {
        get
        {
            if (!TargetAmount.HasValue || !MonthsUntilDue.HasValue || RemainingToTarget <= 0) return null;
            return Math.Round(RemainingToTarget / MonthsUntilDue.Value, 2);
        }
    }

    public decimal? EstimatedAmountByDueDate
    {
        get
        {
            if (!TargetAmount.HasValue || !MonthsUntilDue.HasValue) return null;
            return Math.Round(AllocatedAmount + (DefaultMonthlyContribution * MonthsUntilDue.Value), 2);
        }
    }

    public decimal? ExtraMonthlyContributionNeeded
    {
        get
        {
            if (!SuggestedMonthlyContribution.HasValue) return null;
            return Math.Round(Math.Max(0, SuggestedMonthlyContribution.Value - DefaultMonthlyContribution), 2);
        }
    }

    public decimal RecentMonthlyContribution => Math.Max(0m, DefaultMonthlyContribution);

    public DateTime? EstimatedCompletionDate
    {
        get
        {
            if (!TargetAmount.HasValue || RemainingToTarget <= 0m) return TargetAmount.HasValue ? DateTime.Today : null;
            if (RecentMonthlyContribution <= 0m) return null;
            var months = (int)Math.Ceiling(RemainingToTarget / RecentMonthlyContribution);
            return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(Math.Max(1, months));
        }
    }

    public string GoalTimingLabel
    {
        get
        {
            if (!DueDate.HasValue) return EstimatedCompletionDate.HasValue ? $"Estimated {EstimatedCompletionDate.Value:MMM yyyy}" : "Add contributions to calculate";
            if (!EstimatedCompletionDate.HasValue) return $"Needed by {DueDate.Value:MMM yyyy} · forecast unavailable";
            var monthDifference = ((DueDate.Value.Year - EstimatedCompletionDate.Value.Year) * 12) + DueDate.Value.Month - EstimatedCompletionDate.Value.Month;
            return monthDifference switch
            {
                > 0 => $"On track · estimated {monthDifference} month{(monthDifference == 1 ? string.Empty : "s")} early",
                0 => "On track · estimated in the target month",
                _ => $"Behind target · estimated {Math.Abs(monthDifference)} month{(Math.Abs(monthDifference) == 1 ? string.Empty : "s")} late"
            };
        }
    }
}


public sealed record ReservePotFundingMonth(
    int Year,
    int Month,
    decimal ExpectedAmount,
    decimal ActualAmount,
    decimal AppliedToCurrentMonth,
    decimal AppliedToRecovery,
    decimal CarriedExcessUsed,
    decimal CarriedExcessCreated,
    decimal ShortfallAmount,
    decimal GenuineExcess,
    decimal RecoveryBalance,
    decimal CarriedExcessBalance,
    string Status,
    bool IsPaused,
    DateTime UpdatedAt)
{
    public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public decimal EffectiveCurrentMonthFunding => AppliedToCurrentMonth + CarriedExcessUsed;
    public decimal Difference => EffectiveCurrentMonthFunding - ExpectedAmount;
}

public sealed record ReservePotRecoveryAllocation(
    int SourceYear,
    int SourceMonth,
    int TargetYear,
    int TargetMonth,
    decimal Amount,
    DateTime CreatedAt)
{
    public string SourceMonthLabel => new DateTime(SourceYear, SourceMonth, 1).ToString("MMMM yyyy");
    public string TargetMonthLabel => new DateTime(TargetYear, TargetMonth, 1).ToString("MMMM yyyy");
}

public sealed class ReservePotFundingSummary
{
    public int PotId { get; set; }
    public string CurrentStatus { get; set; } = "Not configured";
    public string CurrentMonthStatus { get; set; } = "Not configured";
    public string StatusCssClass { get; set; } = "muted";
    public decimal CurrentMonthExpected { get; set; }
    public decimal CurrentMonthActual { get; set; }
    public decimal CurrentMonthEffectiveFunding { get; set; }
    public decimal CurrentMonthAppliedToRecovery { get; set; }
    public decimal CurrentMonthCarriedExcessUsed { get; set; }
    public decimal CurrentMonthCarriedExcessCreated { get; set; }
    public decimal CurrentMonthGenuineExcess { get; set; }
    public decimal CurrentMonthRemaining => Math.Max(0, CurrentMonthExpected - CurrentMonthEffectiveFunding);
    public decimal OutstandingRecovery { get; set; }
    public decimal AvailableCarriedExcess { get; set; }
    public decimal ExpectedBalanceToday { get; set; }
    public decimal ActualBalance { get; set; }
    public int MissedMonths { get; set; }
    public int PartiallyFundedMonths { get; set; }
    public decimal ProjectedBalanceByDueDate { get; set; }
    public decimal? ProjectedShortfall { get; set; }
    public decimal? RequiredMonthlyContribution { get; set; }
    public decimal? AdditionalMonthlyContributionRequired { get; set; }
    public decimal RecentMonthlyContribution { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public List<ReservePotFundingMonth> Months { get; set; } = [];
    public List<ReservePotRecoveryAllocation> RecoveryAllocations { get; set; } = [];
}


public sealed class ReserveRecoveryRecommendation
{
    public int PotId { get; set; }
    public string PotName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public decimal OutstandingRecovery { get; set; }
    public decimal NegativeBalanceRecovery { get; set; }
    public bool IsNegativeBalanceRecommendation => NegativeBalanceRecovery > 0m;
    public decimal RecommendedAmount { get; set; }
    public decimal RemainingAfterRecommendation => Math.Max(0m, OutstandingRecovery - RecommendedAmount);
}


public sealed class ApplyRecommendationResult
{
    public bool Succeeded { get; set; }
    public bool WasAlreadyApplied { get; set; }
    public int PotId { get; set; }
    public string PotName { get; set; } = string.Empty;
    public decimal AppliedAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ApplyAllRecommendationsResult
{
    public int AppliedCount { get; set; }
    public decimal TotalApplied { get; set; }
    public List<ApplyRecommendationResult> Results { get; set; } = [];
    public string Message => AppliedCount == 0
        ? "No recommendations were applied."
        : $"Applied {TotalApplied:C} across {AppliedCount} pot{(AppliedCount == 1 ? string.Empty : "s")}.";
}

public sealed record FinanceReminderRow(
    int Id,
    int? ReservePotId,
    string? PotName,
    string Title,
    string? Description,
    DateTime DueDate,
    string ReminderType,
    string Status,
    bool IsSystemGenerated,
    DateTime? SnoozedUntil,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public DateTime EffectiveDueDate => SnoozedUntil?.Date ?? DueDate.Date;
    public bool IsDue => Status == "Open" && EffectiveDueDate <= DateTime.Today;
    public bool IsOverdue => Status == "Open" && EffectiveDueDate < DateTime.Today;
}

public sealed record FinancialTaskItem(
    string Title,
    string Detail,
    string Category,
    string Priority,
    string Url,
    string Icon,
    DateTime? DueDate = null);

public sealed class FinanceRemindersViewModel
{
    public string Status { get; set; } = "Open";
    public List<ReservePot> Pots { get; set; } = [];
    public List<FinanceReminderRow> Reminders { get; set; } = [];
    public List<FinancialTaskItem> SmartTasks { get; set; } = [];
    public int DueCount => Reminders.Count(x => x.IsDue);
    public int OpenCount => Reminders.Count(x => x.Status == "Open") + SmartTasks.Count;
}

public sealed record FinanceEventRow(
    long Id,
    DateTime OccurredAt,
    string Area,
    string EventType,
    string EntityType,
    int? EntityId,
    string Title,
    string? Description,
    decimal? Amount,
    string Source);

public sealed class FinanceEventsViewModel
{
    public int? PotId { get; set; }
    public string? EventType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<ReservePot> Pots { get; set; } = [];
    public List<FinanceEventRow> Events { get; set; } = [];
}

public sealed class HouseholdReserveViewModel
{
    public HouseholdReserve Reserve { get; set; } = new(0, 0, "Money market fund", DateTime.MinValue);
    public HouseholdReserveAccountSummary AccountSummary { get; set; } = new();
    public bool ShowAccountSelector { get; set; }
    public ReserveInterestForecast InterestForecast { get; set; } = new();
    public List<ReservePot> Pots { get; set; } = [];
    public List<ReserveRecoveryRecommendation> RecoveryRecommendations { get; set; } = [];
    public List<FinanceReminderRow> DueReminders { get; set; } = [];
    public Dictionary<int, ReservePotFundingSummary> FundingSummaries { get; set; } = [];
    public decimal TotalAllocated => Pots.Where(p => p.IsActive).Sum(p => Math.Max(0m, p.AllocatedAmount));
    public int ActivePotCount => Pots.Count(p => p.IsActive);
    public decimal RemainingToTargets => Pots.Where(p => p.IsActive && p.TargetAmount.HasValue).Sum(p => Math.Max(0m, p.TargetAmount!.Value - Math.Max(0m, p.AllocatedAmount)));
    public decimal CurrentReserve => AccountSummary.TotalBalance;
    public decimal ProtectedReserve => AccountSummary.Baseline;
    public decimal ReserveShortfall => AccountSummary.BaselineShortfall;
    public decimal AvailableAboveProtectedReserve => AccountSummary.SurplusAboveBaseline;
    public decimal UnallocatedBalance => AvailableAboveProtectedReserve - TotalAllocated;
    public decimal RemainingToAllocate => Math.Max(0m, UnallocatedBalance);
    public decimal AllocatedBeyondAvailable => Math.Max(0m, TotalAllocated - AvailableAboveProtectedReserve);
    public decimal TotalDefaultMonthlyContributions => Pots.Where(p => p.IsActive).Sum(p => p.DefaultMonthlyContribution);
    public decimal EstimatedMonthlyInterest => Math.Round(Reserve.Balance * (Reserve.InterestRate / 100m) / 12m, 2);
    public bool IsOverAllocated => AllocatedBeyondAvailable > 0m;
}



public sealed class ReservePotWithdrawalViewModel
{
    public int PotId { get; set; }
    public string PotName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal Amount { get; set; }
    public DateTime WithdrawalDate { get; set; } = DateTime.Today;
    public string? Reason { get; set; }
    public string OperationKey { get; set; } = Guid.NewGuid().ToString("N");
    public decimal ResultingBalance => CurrentBalance - Amount;
}

public sealed class ReservePotActionResult
{
    public bool Succeeded { get; set; }
    public bool WasAlreadyApplied { get; set; }
    public int PotId { get; set; }
    public string PotName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ResultingBalance { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class MoneyPotActivityViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalAdded { get; set; }
    public int ContributionCount { get; set; }
    public decimal LargestContributionAmount { get; set; }
    public string? LargestContributionPotName { get; set; }
    public string? MostFundedPotName { get; set; }
    public decimal MostFundedPotAmount { get; set; }
    public int CompletedThisMonth { get; set; }
    public int PotsBehindTarget { get; set; }
    public List<MoneyPotActivityRow> Activity { get; set; } = [];
    public List<MoneyPotProgressRow> Pots { get; set; } = [];
    public List<string> Insights { get; set; } = [];
    public DateTime ActivityMonth => new(Year, Month, 1);
    public string MonthLabel => ActivityMonth.ToString("MMMM yyyy");
}

public sealed class MoneyPotActivityRow
{
    public int ContributionId { get; set; }
    public int? PotId { get; set; }
    public string PotName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public bool IsContribution => Amount >= 0m;
}

public sealed class MoneyPotProgressRow
{
    public int PotId { get; set; }
    public string PotName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal? TargetAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal ContributedThisMonth { get; set; }
    public decimal RecentMonthlyContribution { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public bool IsPaused { get; set; }
    public bool IsActive { get; set; }
    public decimal RemainingToTarget => TargetAmount.HasValue ? Math.Max(0m, TargetAmount.Value - CurrentBalance) : 0m;
    public int ProgressPercent => !TargetAmount.HasValue || TargetAmount.Value <= 0m
        ? 0
        : Math.Clamp((int)Math.Round(CurrentBalance / TargetAmount.Value * 100m), 0, 100);
    public bool IsComplete => TargetAmount.HasValue && CurrentBalance >= TargetAmount.Value;
    public bool IsBehindTarget => DueDate.HasValue && EstimatedCompletionDate.HasValue && EstimatedCompletionDate.Value.Date > DueDate.Value.Date;
    public string ForecastLabel
    {
        get
        {
            if (IsComplete) return "Target reached";
            if (!TargetAmount.HasValue) return "No target set";
            if (!EstimatedCompletionDate.HasValue) return "Add more contribution history to estimate";
            if (!DueDate.HasValue) return $"Estimated {EstimatedCompletionDate.Value:MMM yyyy}";
            return IsBehindTarget
                ? $"Estimated {EstimatedCompletionDate.Value:MMM yyyy} · after needed-by date"
                : $"Estimated {EstimatedCompletionDate.Value:MMM yyyy} · on track";
        }
    }
}

public sealed record ExistingPaymentOption(
    string Name,
    decimal Amount,
    string? Category,
    string? Type,
    string? Length,
    string? Notes);

public sealed record MonthlyEntryTemplate(
    int Id,
    string Source,
    string Name,
    decimal DefaultAmount,
    string? Category,
    string? Type,
    string? Length,
    string? Notes,
    bool IsActive);

public sealed class MonthSetupItemInput
{
    public int TemplateId { get; set; }
    public bool Include { get; set; } = true;
    public decimal Amount { get; set; }
}

public sealed class MonthSetupInput
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<MonthSetupItemInput> Items { get; set; } = [];
}


public sealed class PrepareNextMonthSection
{
    public string Source { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<MonthlyEntryTemplate> Items { get; init; } = [];
    public decimal Total => Items.Sum(x => x.DefaultAmount);
}

public sealed class PrepareNextMonthViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime CurrentMonth => new(Year, Month, 1);
    public DateTime NextMonth => CurrentMonth.AddMonths(1);
    public IReadOnlyList<PrepareNextMonthSection> Sections { get; init; } = [];
    public int RecurringEntryCount => Sections.Sum(x => x.Items.Count);
    public decimal RecurringTotal => Sections.Sum(x => x.Total);
    public decimal MonthResult { get; init; }
}

public sealed class CarryOverItemInput
{
    public bool Include { get; set; } = true;
    public string Source { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Length { get; set; }
    public string? Notes { get; set; }
}

public sealed class CarryOverViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime CurrentMonth => new(Year, Month, 1);
    public DateTime NextMonth => CurrentMonth.AddMonths(1);
    public List<CarryOverItemInput> Items { get; set; } = [];
}
