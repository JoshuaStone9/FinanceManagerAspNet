namespace FinanceManagerAspNet.Models;

public sealed record AccountBalance(int Id, string Name, decimal Amount, decimal InterestRate, decimal MonthlyContribution, bool IncludeInGlobalGoal, DateTime UpdatedAt);
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
    public decimal MonthlySavingTarget { get; set; }
    public decimal GlobalGoal { get; set; }
    public decimal MonthlyIncome { get; set; }
    public int SickDays { get; set; }
    public decimal BillsTotal { get; set; }
    public decimal ExpensesTotal { get; set; }
    public decimal InvestmentsTotal { get; set; }
    public decimal SavingsTotal { get; set; }
    public decimal GrandOutgoings => BillsTotal + ExpensesTotal + InvestmentsTotal;
    public decimal RemainingFund => MonthlyIncome - GrandOutgoings + SavingsTotal;
    public decimal CarryOverAmount => Math.Abs(RemainingFund - MonthlySavingTarget);
    public bool IsAhead => RemainingFund >= MonthlySavingTarget;
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
    public List<PaymentRow> Investments { get; set; } = [];
    public List<PaymentRow> Savings { get; set; } = [];
    public List<AccountBalance> Accounts { get; set; } = [];
    public List<LastModifiedInfo> LastModified { get; set; } = [];
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
    DateTime CreatedAt,
    DateTime UpdatedAt);

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
    public SavingPot Pot { get; set; } = new(0, string.Empty, 0, 0, DateTime.MinValue, DateTime.MinValue);
    public List<SavingPotMonth> Months { get; set; } = [];

    public decimal SavedBalance => Months.Where(m => m.IsSaved).Sum(m => m.SavedAmount);

    public decimal Remaining => Math.Max(0, Pot.TargetAmount - SavedBalance);
}

public sealed class SavingPotsViewModel
{
    public int Year { get; set; }
    public decimal EmergencyFundTotal { get; set; }
    public decimal AllocatedToPots { get; set; }
    public decimal AvailableEmergencyFund => Math.Round(EmergencyFundTotal - AllocatedToPots, 2);
    public List<SavingPotRowViewModel> Pots { get; set; } = [];
}

public sealed class StatisticsViewModel
{
    public decimal ManualAverageIncome { get; set; }
    public decimal CalculatedSalaryEstimate { get; set; }
    public decimal AverageSavingPace { get; set; }
    public decimal MonthlySavingTarget { get; set; }
    public decimal GlobalGoal { get; set; }
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