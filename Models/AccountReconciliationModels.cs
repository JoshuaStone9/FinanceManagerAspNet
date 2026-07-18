namespace FinanceManagerAspNet.Models;

public sealed class AccountReconciliationViewModel
{
    public IReadOnlyList<AccountReconciliationRow> Accounts { get; init; } = [];
}

public sealed record AccountReconciliationRow(
    int AccountId,
    string SourceKey,
    string AccountName,
    decimal RecordedBalance,
    decimal InterestRate,
    decimal MonthlyContribution,
    bool IncludeInForecast,
    DateTime? LastReconciledAt,
    DateTime BalanceUpdatedAt);
