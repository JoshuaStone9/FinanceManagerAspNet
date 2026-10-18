using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FinanceManagerAspNet.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FinanceManagerAspNet.Services;

public interface IStatementImportService
{
    Task<IReadOnlyList<ParsedStatementTransaction>> ExtractAsync(string path, string parser, int year, int month, CancellationToken cancellationToken = default);
}

public sealed partial class StatementImportService : IStatementImportService
{
    private static readonly string[] DateFormats =
    [
        "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "d/M/yy", "dd/MM/yy",
        "d MMM yyyy", "dd MMM yyyy", "d MMM yy", "dd MMM yy", "d MMM", "dd MMM"
    ];

    public Task<IReadOnlyList<ParsedStatementTransaction>> ExtractAsync(
        string path, string parser, int year, int month, CancellationToken cancellationToken = default)
    {
        var rows = new List<ParsedStatementTransaction>();
        if (parser.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IReadOnlyList<ParsedStatementTransaction>>(rows);
        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = ContentOrderTextExtractor.GetText(page);
            foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryParseLine(rawLine, parser, year, month, out var row)) rows.Add(row);
            }
        }

        return Task.FromResult<IReadOnlyList<ParsedStatementTransaction>>(rows
            .GroupBy(x => x.Fingerprint, StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Description)
            .ToList());
    }

    public static bool TryParseLine(string rawLine, string parser, int statementYear, int statementMonth, out ParsedStatementTransaction transaction)
    {
        transaction = default!;
        var line = WhitespaceRegex().Replace(rawLine, " ").Trim();
        var match = TransactionLineRegex().Match(line);
        if (!match.Success || !TryParseDate(match.Groups["date"].Value, statementYear, statementMonth, out var date)) return false;

        var description = match.Groups["description"].Value.Trim(' ', '-', ':');
        var amounts = MoneyRegex().Matches(match.Groups["amounts"].Value).Select(x => x.Value).ToList();
        if (description.Length < 2 || amounts.Count == 0) return false;

        var selected = amounts[^1];
        var amountText = selected.Replace("£", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
        var negative = amountText.StartsWith('-') || amountText.StartsWith('(') || amountText.EndsWith(')');
        amountText = amountText.Trim('-', '+', '(', ')');
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return false;

        var direction = ResolveDirection(line, parser, negative, amounts.Count);
        var fingerprint = CreateFingerprint(date, description, amount, direction);
        transaction = new ParsedStatementTransaction(date, description, amount, direction, fingerprint);
        return true;
    }

    private static string ResolveDirection(string line, string parser, bool negative, int amountCount)
    {
        if (negative) return "Out";
        if (CreditRegex().IsMatch(line)) return "In";
        if (DebitRegex().IsMatch(line)) return "Out";
        if (parser.Equals("Monzo", StringComparison.OrdinalIgnoreCase) || parser.Equals("Starling", StringComparison.OrdinalIgnoreCase))
            return line.Contains('+') ? "In" : "Out";
        // Two trailing money columns are commonly debit then credit; a blank debit
        // column is lost during extraction, so unknown layouts remain conservative.
        return amountCount > 1 && line.Contains("  ", StringComparison.Ordinal) ? "In" : "Out";
    }

    private static bool TryParseDate(string value, int year, int month, out DateTime date)
    {
        var culture = CultureInfo.GetCultureInfo("en-GB");
        if (DateTime.TryParseExact(value, DateFormats, culture, DateTimeStyles.AllowWhiteSpaces, out date)
            || DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out date))
        {
            if (!value.Any(char.IsDigit) || value.Count(char.IsDigit) <= 2)
                date = new DateTime(year, date.Month, date.Day);
            else if (!value.Contains(year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) && date.Year < 100)
                date = new DateTime(year, date.Month, date.Day);
            return date.Year == year && date.Month == month;
        }
        date = default;
        return false;
    }

    public static string CreateFingerprint(DateTime date, string description, decimal amount, string direction)
    {
        var normalized = NonAlphaNumericRegex().Replace(description.Trim().ToUpperInvariant(), "");
        var value = $"{date:yyyy-MM-dd}|{normalized}|{amount:0.00}|{direction}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"^(?<date>\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?|\d{1,2}\s+[A-Za-z]{3}(?:\s+\d{2,4})?)\s+(?<description>.+?)\s+(?<amounts>(?:[-+]?[£]?(?:\d{1,3}(?:,\d{3})*|\d+)[.]\d{2}[)]?\s*)+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TransactionLineRegex();
    [GeneratedRegex(@"[-+]?[£]?(?:\d{1,3}(?:,\d{3})*|\d+)[.]\d{2}[)]?")]
    private static partial Regex MoneyRegex();
    [GeneratedRegex(@"\b(CR|CREDIT|PAID IN|DEPOSIT|INTEREST)\b|\+£?\d", RegexOptions.IgnoreCase)]
    private static partial Regex CreditRegex();
    [GeneratedRegex(@"\b(DR|DEBIT|CARD|PAYMENT|DIRECT DEBIT|WITHDRAWAL|PURCHASE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DebitRegex();
    [GeneratedRegex(@"[^A-Z0-9]")]
    private static partial Regex NonAlphaNumericRegex();
}
