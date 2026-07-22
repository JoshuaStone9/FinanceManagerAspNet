namespace FinanceManagerAspNet.Models;

public sealed class AccountReconciliationViewModel
{
    public decimal BalanceTolerance { get; init; } = 5m;
    public IReadOnlyList<AccountReconciliationRow> Accounts { get; init; } = [];
    public IReadOnlyList<EmergencyFundTransactionRow> EmergencyFundTransactions { get; init; } = [];
    public decimal SelectedAccountsTotal { get; init; }
    public decimal BaseReserveAmount { get; init; }
    public decimal LoggedVirtualPotsTotal { get; init; }
    public decimal ExpectedReserveTotal { get; init; }
    public decimal ReserveDifference { get; init; }
    public string ReserveReconciliationStatus { get; init; } = "Balanced";
    public bool HasReserveConcern => ReserveReconciliationStatus is "More than logged" or "Less than logged";
    public int AccountsNeedingInterestReconciliation => Accounts.Count(x => x.PendingInterestCount > 0);
    public decimal TotalPendingInterest => Accounts.Sum(x => x.PendingInterest);
    public DateTime? OldestPendingInterestDate => Accounts
        .Where(x => x.OldestPendingInterestDate.HasValue)
        .Select(x => x.OldestPendingInterestDate)
        .Min();
    public AccountReconciliationRow? FirstAccountNeedingReconciliation => Accounts
        .Where(x => x.NeedsInterestReconciliation)
        .OrderBy(x => x.OldestPendingInterestDate ?? DateTime.MaxValue)
        .ThenBy(x => x.AccountName)
        .FirstOrDefault();
}

public sealed record AccountReconciliationRow(
    int AccountId,
    string SourceKey,
    string AccountName,
    decimal RecordedBalance,
    decimal InterestRate,
    decimal MonthlyContribution,
    bool IncludeInForecast,
    decimal StartingBalance,
    string Provider,
    string AccountType,
    string HoldingType,
    string TaxTreatment,
    decimal TaxRate,
    DateTime? TaxEffectiveFrom,
    DateTime? LastReconciledAt,
    DateTime BalanceUpdatedAt,
    decimal PendingInterest,
    int PendingInterestCount,
    DateTime? OldestPendingInterestDate,
    string InterestHandling,
    decimal ExpectedBalance,
    decimal Difference,
    string ReconciliationStatus,
    IReadOnlyList<AccountRecentActivityRow> RecentActivities)
{
    public decimal SuggestedBalance => Math.Round(RecordedBalance + PendingInterest, 2);
    public bool NeedsInterestReconciliation => PendingInterestCount > 0 && PendingInterest > 0m;
    public bool HasBalanceConcern => ReconciliationStatus is "More than logged" or "Less than logged";
}


public sealed record AccountRecentActivityRow(
    DateTime OccurredAt,
    string Title,
    string EventType,
    decimal? Amount,
    string Source);

public sealed record AccountBalanceUpdateResult(
    decimal ReconciledInterest,
    decimal ExpectedBalance,
    decimal ActualBalance,
    decimal Difference,
    string Status,
    string AccountName);

public sealed record EmergencyFundTransactionRow(
    long TransactionId,
    string TransactionType,
    decimal Amount,
    DateTime OccurredAt,
    string? Note,
    long? ReversedTransactionId,
    long? ReversedByTransactionId)
{
    public bool CanReverse => TransactionType == "Contribution" && ReversedByTransactionId is null;
    public bool IsReversed => ReversedByTransactionId is not null;
}
