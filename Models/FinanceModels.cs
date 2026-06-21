namespace FinanceManagerAspNet.Models;

public sealed record PaymentRow(int Id, string Name, decimal Amount, DateTime Date, string? Category, string? Type, string? Length, string? Notes, string Source);
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
}
