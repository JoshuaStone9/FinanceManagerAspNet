using System.Net;
using System.Text.RegularExpressions;

namespace MoveInPlanner.Services.ProductMetadata;

public sealed partial class AmazonProductMetadataProvider : IRetailerProductMetadataProvider
{
    private static readonly Uri ImageReferer = new("https://www.amazon.co.uk/");

    public string RetailerName => "Amazon UK";

    public bool Supports(Uri uri) => AllowsPageUri(uri);

    public bool AllowsPageUri(Uri uri) => RetailerUrlMatcher.UsesHttp(uri) && RetailerUrlMatcher.HostMatches(uri.Host, "amzn.eu", "amazon.co.uk", "amazon.com");

    public Uri? GetImageReferer(Uri imageUri) =>
        RetailerUrlMatcher.HostMatches(imageUri.Host, "media-amazon.com", "ssl-images-amazon.com", "images-amazon.com", "amazon.co.uk", "amazon.com")
            ? ImageReferer : null;

    public ExtractedProductMetadata Enrich(ProductMetadataPage page, ExtractedProductMetadata metadata)
    {
        var html = page.Html;

        var name = ProductMetadataHtmlReader.FirstNonEmpty(metadata.Name, ProductMetadataHtmlReader.ReadElementText(html, "productTitle"));

        var imageUrl = ProductMetadataHtmlReader.FirstNonEmpty(metadata.ImageUrl,
            ReadLandingImageAttribute(html, "data-old-hires"),
            ReadLandingImageAttribute(html, "src"),
            ReadDynamicImage(html),
            ReadAmazonImageFromPageJson(html));

        var price = metadata.Price ?? ProductMetadataHtmlReader.ReadFirstPrice(ReadAmazonPrice(html));

        return metadata with
        {
            Name = CleanAmazonTitle(name),
            Price = price,
            ImageUrl = ProductMetadataHtmlReader.NormaliseImageUrl(imageUrl)
        };
    }

    private static string? ReadLandingImageAttribute(string html, string attributeName)
    {
        var match = LandingImageRegex().Match(html);

        if (!match.Success) 
            return null;

        return ProductMetadataHtmlReader.Decode(ProductMetadataHtmlReader.ReadAttribute(match.Value, attributeName));
    }

    private static string? ReadDynamicImage(string html)
    {
        var match = DynamicImageRegex().Match(html);

        if (!match.Success) 
            return null;

        var jsonish = WebUtility.HtmlDecode(match.Groups["value"].Value)
            .Replace("\\/", "/");

        return ProductMetadataHtmlReader.ReadFirstImageUrl(jsonish);
    }

    private static string? ReadAmazonImageFromPageJson(string html)
    {
        var decoded = WebUtility.HtmlDecode(html).Replace("\\/", "/");
        var candidates = new[]
        {
            "hiRes",
            "large",
            "mainUrl",
            "landingImage",
            "imageUrl"
        };

        foreach (var key in candidates)
        {
            var pattern = $"[\\\"']{Regex.Escape(key)}[\\\"']\\s*:\\s*[\\\"'](?<url>https://[^\\\"']+)[\\\"']";
            var match = Regex.Match(decoded, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (match.Success)
                return match.Groups["url"].Value;
        }

        return ProductMetadataHtmlReader.ReadFirstImageUrl(decoded);
    }

    private static string? ReadAmazonPrice(string html)
    {
        var offscreen = PriceOffscreenRegex().Match(html);

        if (offscreen.Success)
            return ProductMetadataHtmlReader.Decode(ProductMetadataHtmlReader.StripTags(offscreen.Groups["value"].Value));

        var whole = PriceWholeRegex().Match(html);

        if (!whole.Success) 
            return null;

        var fraction = PriceFractionRegex().Match(html, whole.Index + whole.Length);

        var wholeValue = ProductMetadataHtmlReader.StripTags(whole.Groups["value"].Value);

        return fraction.Success ? $"{wholeValue}.{ProductMetadataHtmlReader.StripTags(fraction.Groups["value"].Value)}" : wholeValue;
    }

    private static string? CleanAmazonTitle(string? title)
    {
        var cleaned = ProductMetadataHtmlReader.CleanTitle(title);

        if (cleaned is null) 
            return null;

        return Regex.Replace(cleaned, @"\s*[:|–-]\s*Amazon(?:\.co\.uk)?\s*$", string.Empty, RegexOptions.IgnoreCase);
    }

    [GeneratedRegex("""data-a-dynamic-image\s*=\s*["'](?<value>.*?)["']""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DynamicImageRegex();

    [GeneratedRegex("""<img\b[^>]*\bid\s*=\s*["']landingImage["'][^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LandingImageRegex();

    [GeneratedRegex("""<span\b[^>]*class\s*=\s*["'][^"']*a-offscreen[^"']*["'][^>]*>(?<value>.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceOffscreenRegex();

    [GeneratedRegex("""<span\b[^>]*class\s*=\s*["'][^"']*a-price-whole[^"']*["'][^>]*>(?<value>.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceWholeRegex();

    [GeneratedRegex("""<span\b[^>]*class\s*=\s*["'][^"']*a-price-fraction[^"']*["'][^>]*>(?<value>.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceFractionRegex();
}
