using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace MoveInPlanner.Services.ProductMetadata;

public static partial class ProductMetadataHtmlReader
{
    public static ExtractedProductMetadata ReadCommon(string html)
    {
        var name = FirstNonEmpty(
            ReadMeta(html, "property", "og:title"),
            ReadMeta(html, "name", "title"),
            ReadTitle(html));

        var imageUrl = FirstNonEmpty(
            ReadMeta(html, "property", "og:image"),
            ReadMeta(html, "name", "twitter:image"));

        var price = ReadFirstPrice(
            ReadMeta(html, "property", "product:price:amount"),
            ReadMeta(html, "name", "price"),
            ReadItemPropPrice(html));

        return new ExtractedProductMetadata(
            Name: CleanTitle(name),
            Price: price,
            ImageUrl: NormaliseImageUrl(imageUrl));
    }

    public static string? ReadMeta(string html, string attributeName, string attributeValue)
    {
        foreach (Match match in MetaTagRegex().Matches(html))
        {
            var tag = match.Value;
            var key = ReadAttribute(tag, attributeName);

            if (string.Equals(
                    key,
                    attributeValue,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Decode(ReadAttribute(tag, "content"));
            }
        }

        return null;
    }

    public static string? ReadAttribute(string tag, string attributeName)
    {
        var pattern = $$"""\b{{Regex.Escape(attributeName)}}\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s>]+))""";
        var match = Regex.Match(tag, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups["value"].Value : null;
    }

    public static string? ReadElementText(string html, string id)
    {
        var pattern = $$"""<(?<tag>[a-z0-9]+)[^>]*\bid\s*=\s*["']{{Regex.Escape(id)}}["'][^>]*>(?<value>.*?)</\k<tag>>""";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? Decode(StripTags(match.Groups["value"].Value)) : null;
    }

    public static string? ReadFirstImageUrl(string value)
    {
        var match = FirstImageUrlRegex().Match(value);
        return match.Success ? match.Value : null;
    }

    public static decimal? ReadFirstPrice(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (TryParsePrice(candidate, out var price))
                return price;
        }

        return null;
    }

    public static bool TryParsePrice(string? value, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var cleaned = Regex.Replace(value, @"[^0-9.,]", string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(cleaned)) 
            return false;

        if (cleaned.Contains(',') && cleaned.Contains('.'))
            cleaned = cleaned.Replace(",", string.Empty);
        else if (cleaned.Count(character => character == ',') == 1 &&
                 !cleaned.Contains('.'))
            cleaned = cleaned.Replace(',', '.');
        else
            cleaned = cleaned.Replace(",", string.Empty);

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out price);
    }

    public static string? CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var cleaned = Regex.Replace(title, @"\s+", " ").Trim();
        return cleaned.Length > 200 ? cleaned[..200].Trim() : cleaned;
    }

    public static string? NormaliseImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var decoded = Decode(imageUrl)?.Replace("\\/", "/").Trim();

        return Uri.TryCreate(decoded, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;
    }

    public static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public static string? Decode(string? value) => string.IsNullOrWhiteSpace(value)
            ? null
            : WebUtility.HtmlDecode(value).Trim();

    public static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty);

    private static string? ReadTitle(string html)
    {
        var match = TitleRegex().Match(html);

        return match.Success ? Decode(StripTags(match.Groups["value"].Value)) : null;
    }

    private static string? ReadItemPropPrice(string html)
    {
        var match = ItemPropPriceRegex().Match(html);
        return match.Success ? ReadAttribute(match.Value, "content") : null;
    }

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(@"<title\b[^>]*>(?<value>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("""<meta\b[^>]*\bitemprop\s*=\s*["']price["'][^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ItemPropPriceRegex();

    [GeneratedRegex("""https://[^\"'<>\s]+?\.(?:jpg|jpeg|png|webp)(?:\?[^\"'<>\s]*)?""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirstImageUrlRegex();
}
