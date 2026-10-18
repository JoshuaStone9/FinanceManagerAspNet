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
    public string StatementParser { get; init; } = "Generic";
    public IReadOnlyList<StatementTransactionRow> Transactions { get; init; } = [];
    public IReadOnlyList<StatementImportPreviewRow> ImportPreview { get; init; } = [];
    public IReadOnlyList<FinanceEntryCandidate> Candidates { get; init; } = [];
    public int MatchedCount => Transactions.Count(x => x.Status == "Matched");
    public int UnmatchedCount => Transactions.Count(x => x.Status == "Unmatched");
    public int IgnoredCount => Transactions.Count(x => x.Status == "Ignored");
    public decimal StatementNet => Transactions.Sum(x => x.Direction == "In" ? x.Amount : -x.Amount);
    public bool CanComplete => Transactions.Count > 0 && UnmatchedCount == 0;
    public bool IsCompleted => Status == "Completed";
}

public sealed record StatementTransactionRow(long Id, DateTime TransactionDate, string Description, decimal Amount, string Direction, string Status, string? MatchedSource, int? MatchedEntryId, string? MatchLabel, string? Notes);
public sealed record ParsedStatementTransaction(DateTime Date, string Description, decimal Amount, string Direction, string Fingerprint);
public sealed record StatementImportPreviewRow(long Id, DateTime Date, string Description, decimal Amount, string Direction, bool IsDuplicate, string Fingerprint);
public sealed record StatementMatchSuggestion(FinanceEntryCandidate Candidate, int Score, string Reason);
public sealed record FinanceEntryCandidate(string Key, string Source, int EntryId, DateTime Date, string Name, decimal Amount, string Direction)
{
    public string Label => $"{Date:dd MMM} · {Name} · {(Direction == "In" ? "+" : "-")}{Amount:C}";
}

public static class StatementMatchScorer
{
    public static IReadOnlyList<StatementMatchSuggestion> Rank(StatementTransactionRow transaction, IEnumerable<FinanceEntryCandidate> candidates)
        => candidates
            .Where(x => x.Direction == transaction.Direction)
            .Select(x => Score(transaction, x))
            .Where(x => x.Score >= 40)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => Math.Abs((x.Candidate.Date - transaction.TransactionDate).Days))
            .Take(3)
            .ToList();

    private static StatementMatchSuggestion Score(StatementTransactionRow transaction, FinanceEntryCandidate candidate)
    {
        var score = 0;
        var reasons = new List<string>();
        if (transaction.Amount == candidate.Amount) { score += 60; reasons.Add("same amount"); }
        else
        {
            var difference = Math.Abs(transaction.Amount - candidate.Amount);
            if (difference <= 0.01m) { score += 55; reasons.Add("near-identical amount"); }
            else if (transaction.Amount > 0 && difference / transaction.Amount <= 0.05m) { score += 25; reasons.Add("similar amount"); }
        }
        var days = Math.Abs((candidate.Date.Date - transaction.TransactionDate.Date).Days);
        if (days == 0) { score += 25; reasons.Add("same date"); }
        else if (days <= 3) { score += 15; reasons.Add($"{days} day{(days == 1 ? "" : "s")} apart"); }
        else if (days <= 7) { score += 5; reasons.Add("within a week"); }

        var descriptionWords = Words(transaction.Description);
        var nameWords = Words(candidate.Name);
        if (descriptionWords.Overlaps(nameWords)) { score += 15; reasons.Add("similar description"); }
        return new StatementMatchSuggestion(candidate, Math.Min(score, 100), string.Join(", ", reasons));
    }

    private static HashSet<string> Words(string value) => value
        .Split([' ', '-', '/', '.', ',', '*'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => x.Length >= 3)
        .Select(x => x.ToUpperInvariant())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
