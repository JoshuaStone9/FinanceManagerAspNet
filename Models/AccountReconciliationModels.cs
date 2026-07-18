namespace FinanceManagerAspNet.Models;

public sealed class AccountReconciliationViewModel
{
    public IReadOnlyList<AccountReconciliationRow> Accounts { get; init; } = [];
}

public sealed record AccountReconciliationRow(
    string SourceKey,
    string AccountName,
    decimal RecordedBalance,
    DateTime? LastReconciledAt,
    DateTime BalanceUpdatedAt);
