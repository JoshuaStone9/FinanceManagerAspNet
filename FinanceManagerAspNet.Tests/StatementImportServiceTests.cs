using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class StatementImportServiceTests
{
    [Fact]
    public void TryParseLine_ReadsTypicalDebit()
    {
        var parsed = StatementImportService.TryParseLine(
            "14/09/2026 TESCO STORES CARD £42.19",
            "Generic",
            2026,
            9,
            out var row);

        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 9, 14), row.Date);
        Assert.Equal("TESCO STORES CARD", row.Description);
        Assert.Equal(42.19m, row.Amount);
        Assert.Equal("Out", row.Direction);
    }

    [Fact]
    public void TryParseLine_ReadsCreditAndRejectsWrongMonth()
    {
        Assert.True(StatementImportService.TryParseLine(
            "26 Sep 2026 SALARY CREDIT +£2,300.00", "Generic", 2026, 9, out var row));
        Assert.Equal("In", row.Direction);
        Assert.Equal(2300m, row.Amount);

        Assert.False(StatementImportService.TryParseLine(
            "26 Oct 2026 SALARY CREDIT +£2,300.00", "Generic", 2026, 9, out _));
    }

    [Fact]
    public void Fingerprint_IsStableAcrossDescriptionFormatting()
    {
        var first = StatementImportService.CreateFingerprint(new DateTime(2026, 9, 14), "Tesco Stores", 42.19m, "Out");
        var second = StatementImportService.CreateFingerprint(new DateTime(2026, 9, 14), "TESCO-STORES", 42.19m, "Out");
        Assert.Equal(first, second);
    }

    [Fact]
    public void MatchScorer_PrioritisesSameAmountDateAndDescription()
    {
        var transaction = new StatementTransactionRow(1, new DateTime(2026, 9, 14), "Tesco card payment", 42.19m, "Out", "Unmatched", null, null, null, null);
        var candidates = new[]
        {
            new FinanceEntryCandidate("bills:1", "bills", 1, new DateTime(2026, 9, 20), "Energy", 42.19m, "Out"),
            new FinanceEntryCandidate("everyday_spending:2", "everyday_spending", 2, new DateTime(2026, 9, 14), "Tesco groceries", 42.19m, "Out")
        };

        var ranked = StatementMatchScorer.Rank(transaction, candidates);

        Assert.Equal("everyday_spending:2", ranked[0].Candidate.Key);
        Assert.Equal(100, ranked[0].Score);
    }
}
