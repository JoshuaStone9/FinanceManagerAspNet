namespace FinanceManagerAspNet.Models;

public sealed class StatementReconciliationIndexViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public IReadOnlyList<StatementAccountSummary> Accounts { get; init; } = [];
}

public sealed record StatementAccountSummary(int AccountId, string AccountName, string Provider, string MaskedReference, bool IsActive, long? StatementId, string Status, int TransactionCount, int MatchedCount, int UnmatchedCount, int IgnoredCount, DateTime? UploadedAt);

public sealed class StatementWorkspaceViewModel
{
    public long StatementId { get; init; }
    public int AccountId { get; init; }
    public string AccountName { get; init; } = "";
    public string Provider { get; init; } = "";
    public int Year { get; init; }
    public int Month { get; init; }
    public string Status { get; init; } = "Draft";
    public string? OriginalFileName { get; init; }
    public DateTime? UploadedAt { get; init; }
    public IReadOnlyList<StatementTransactionRow> Transactions { get; init; } = [];
    public IReadOnlyList<FinanceEntryCandidate> Candidates { get; init; } = [];
    public int MatchedCount => Transactions.Count(x => x.Status == "Matched");
    public int UnmatchedCount => Transactions.Count(x => x.Status == "Unmatched");
    public int IgnoredCount => Transactions.Count(x => x.Status == "Ignored");
    public decimal StatementNet => Transactions.Sum(x => x.Direction == "In" ? x.Amount : -x.Amount);
}

public sealed record StatementTransactionRow(long Id, DateTime TransactionDate, string Description, decimal Amount, string Direction, string Status, string? MatchedSource, int? MatchedEntryId, string? MatchLabel, string? Notes);
public sealed record FinanceEntryCandidate(string Key, string Source, int EntryId, DateTime Date, string Name, decimal Amount, string Direction)
{
    public string Label => $"{Date:dd MMM} · {Name} · {(Direction == "In" ? "+" : "-")}{Amount:C}";
}
