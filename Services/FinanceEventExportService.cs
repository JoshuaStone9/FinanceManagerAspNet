using System.Globalization;
using System.Text;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public sealed class FinanceEventExportService
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public byte[] BuildCsv(IReadOnlyList<FinanceEventRow> events)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Occurred At,Area,Event Type,Entity Type,Entity ID,Title,Description,Amount,Direction,Source");
        foreach (var item in events)
        {
            var direction = item.Amount switch
            {
                > 0m => "In",
                < 0m => "Out",
                _ => string.Empty
            };
            csv.AppendLine(string.Join(',', new[]
            {
                Escape(item.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Escape(item.Area), Escape(item.EventType), Escape(item.EntityType),
                Escape(item.EntityId?.ToString(CultureInfo.InvariantCulture)), Escape(item.Title),
                Escape(item.Description), Escape(item.Amount?.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(direction), Escape(item.Source)
            }));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public byte[] BuildPdf(IReadOnlyList<FinanceEventRow> events, string filterSummary)
    {
        var lines = new List<string>
        {
            "Finance Manager - Event Report",
            $"Generated: {DateTime.Now:dd MMMM yyyy HH:mm}",
            filterSummary,
            $"Events: {events.Count}",
            string.Empty
        };

        foreach (var item in events)
        {
            var amount = item.Amount.HasValue ? $" | {item.Amount.Value.ToString("C", Gb)}" : string.Empty;
            lines.Add($"{item.OccurredAt.ToLocalTime():dd MMM yyyy HH:mm} | {item.Area} | {item.EventType}{amount}");
            lines.Add(item.Title);
            if (!string.IsNullOrWhiteSpace(item.Description)) lines.Add(item.Description!);
            lines.Add($"Entity: {item.EntityType}{(item.EntityId.HasValue ? $" #{item.EntityId}" : string.Empty)} | Source: {item.Source}");
            lines.Add(string.Empty);
        }

        return SimplePdfWriter.Create(lines);
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static class SimplePdfWriter
    {
        public static byte[] Create(IReadOnlyList<string> inputLines)
        {
            const int linesPerPage = 48;
            var wrapped = inputLines.SelectMany(line => Wrap(line, 95)).ToList();
            var pages = wrapped.Chunk(linesPerPage).ToList();
            if (pages.Count == 0) pages.Add([]);

            var objects = new List<string>();
            var pageObjectIds = new List<int>();
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add(string.Empty); // pages object populated later
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            foreach (var page in pages)
            {
                var contentId = objects.Count + 1;
                var stream = BuildPageStream(page);
                objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
                var pageId = objects.Count + 1;
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>");
                pageObjectIds.Add(pageId);
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>";

            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\n" };
            writer.Write("%PDF-1.4\n");
            writer.Flush();
            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(output.Position);
                writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
                writer.Flush();
            }
            var xref = output.Position;
            writer.Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1)) writer.Write($"{offset:0000000000} 00000 n \n");
            writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
            writer.Flush();
            return output.ToArray();
        }

        private static string BuildPageStream(IEnumerable<string> lines)
        {
            var sb = new StringBuilder("BT\n/F1 9 Tf\n45 800 Td\n12 TL\n");
            foreach (var line in lines)
                sb.Append('(').Append(EscapePdf(line)).Append(") Tj\nT*\n");
            sb.Append("ET");
            return sb.ToString();
        }

        private static IEnumerable<string> Wrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return [string.Empty];
            var result = new List<string>();
            while (text.Length > width)
            {
                var split = text.LastIndexOf(' ', width);
                if (split <= 0) split = width;
                result.Add(text[..split]);
                text = text[split..].TrimStart();
            }
            result.Add(text);
            return result;
        }

        private static string EscapePdf(string value) => value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Select(ch => ch <= 126 ? ch : '?')
            .Aggregate(new StringBuilder(), (sb, ch) => sb.Append(ch)).ToString();
    }
}
