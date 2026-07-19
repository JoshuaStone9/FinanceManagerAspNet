namespace FinanceManagerAspNet.Models;

public sealed class AccountReconciliationViewModel
{
    public IReadOnlyList<AccountReconciliationRow> Accounts { get; init; } = [];
    public int AccountsNeedingInterestReconciliation => Accounts.Count(x => x.PendingInterestCount > 0);
    public decimal TotalPendingInterest => Accounts.Sum(x => x.PendingInterest);
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
    string InterestHandling)
{
    public decimal SuggestedBalance => Math.Round(RecordedBalance + PendingInterest, 2);
    public bool NeedsInterestReconciliation => PendingInterestCount > 0 && PendingInterest > 0m;
}
