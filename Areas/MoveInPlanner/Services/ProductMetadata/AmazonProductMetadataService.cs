using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MoveInPlanner.Models.Results;

namespace MoveInPlanner.Services.ProductMetadata;

/// <summary>
/// Provides a best-effort import of basic metadata exposed in an Amazon product page.
/// This is intentionally isolated behind an interface because retailer HTML may change.
/// </summary>
public sealed partial class AmazonProductMetadataService(HttpClient httpClient)
    : IProductMetadataService
{
    private const int MaximumHtmlCharacters = 2_000_000;

    public async Task<ProductMetadataResult> GetFromUrlAsync(
        string productUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryCreateAllowedUri(productUrl, out var requestedUri))
        {
            return Failure("Enter a valid Amazon or amzn.eu product URL.");
        }

        try
        {
            using var response = await SendFollowingAllowedRedirectsAsync(
                requestedUri,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Failure($"Amazon returned HTTP {(int)response.StatusCode}. Enter the details manually or try again later.");
            }

            var finalUri = response.RequestMessage?.RequestUri ?? requestedUri;

            var html = await ReadLimitedHtmlAsync(response, cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return Failure("Amazon returned an empty page.");
            }

            var title = FirstNonEmpty(
                ReadMeta(html, "property", "og:title"),
                ReadMeta(html, "name", "title"),
                ReadElementText(html, "productTitle"),
                ReadTitle(html));

            var imageUrl = FirstNonEmpty(
                ReadMeta(html, "property", "og:image"),
                ReadMeta(html, "name", "twitter:image"),
                ReadLandingImageAttribute(html, "data-old-hires"),
                ReadLandingImageAttribute(html, "src"),
                ReadDynamicImage(html),
                ReadAmazonImageFromPageJson(html));

            var price = ReadPrice(html);

            title = CleanTitle(title);
            imageUrl = NormaliseImageUrl(imageUrl);

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(imageUrl) && price is null)
            {
                return Failure("Amazon did not expose product metadata on this request. The fields can still be entered manually.");
            }

            return new ProductMetadataResult
            {
                Success = true,
                Name = title,
                Price = price,
                ImageUrl = imageUrl,
                Retailer = "Amazon UK",
                ResolvedUrl = finalUri.ToString()
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("The Amazon request timed out. Try again or enter the details manually.");
        }
        catch (HttpRequestException)
        {
            return Failure("Amazon could not be reached. Check the URL or enter the details manually.");
        }
        catch (Exception)
        {
            return Failure("The product details could not be imported. Enter them manually.");
        }
    }

    private async Task<HttpResponseMessage> SendFollowingAllowedRedirectsAsync(
        Uri startingUri,
        CancellationToken cancellationToken)
    {
        var currentUri = startingUri;

        for (var redirectCount = 0; redirectCount <= 5; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-GB,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");

            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!IsRedirect(response.StatusCode)) return response;

            var location = response.Headers.Location;
            response.Dispose();

            if (location is null)
                throw new HttpRequestException("Amazon returned a redirect without a destination.");

            var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
            if (nextUri.Scheme is not ("http" or "https") || !IsAllowedAmazonHost(nextUri.Host))
                throw new HttpRequestException("Amazon redirected outside an approved domain.");

            currentUri = nextUri;
        }

        throw new HttpRequestException("Amazon redirected too many times.");
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static async Task<string> ReadLimitedHtmlAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[16_384];
        var result = new System.Text.StringBuilder();

        while (result.Length < MaximumHtmlCharacters)
        {
            var remaining = Math.Min(buffer.Length, MaximumHtmlCharacters - result.Length);
            var read = await reader.ReadAsync(buffer.AsMemory(0, remaining), cancellationToken);
            if (read == 0) break;
            result.Append(buffer, 0, read);
        }

        return result.ToString();
    }

    private static bool TryCreateAllowedUri(string value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var candidate)) return false;
        if (candidate.Scheme is not ("http" or "https")) return false;
        if (!IsAllowedAmazonHost(candidate.Host)) return false;
        uri = candidate;
        return true;
    }

    private static bool IsAllowedAmazonHost(string host)
    {
        var value = host.TrimEnd('.').ToLowerInvariant();
        return value == "amzn.eu" ||
               value == "amazon.co.uk" || value.EndsWith(".amazon.co.uk") ||
               value == "amazon.com" || value.EndsWith(".amazon.com");
    }

    private static string? ReadMeta(string html, string attributeName, string attributeValue)
    {
        foreach (Match match in MetaTagRegex().Matches(html))
        {
            var tag = match.Value;
            var key = ReadAttribute(tag, attributeName);
            if (!string.Equals(key, attributeValue, StringComparison.OrdinalIgnoreCase)) continue;
            return Decode(ReadAttribute(tag, "content"));
        }

        return null;
    }

    private static string? ReadAttribute(string tag, string attributeName)
    {
        var pattern = $$"""\b{{Regex.Escape(attributeName)}}\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s>]+))""";
        var match = Regex.Match(tag, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ReadElementText(string html, string id)
    {
        var pattern = $$"""<(?<tag>[a-z0-9]+)[^>]*\bid\s*=\s*["']{{Regex.Escape(id)}}["'][^>]*>(?<value>.*?)</\k<tag>>""";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? Decode(StripTags(match.Groups["value"].Value)) : null;
    }

    private static string? ReadTitle(string html)
    {
        var match = TitleRegex().Match(html);
        return match.Success ? Decode(StripTags(match.Groups["value"].Value)) : null;
    }

    private static string? ReadLandingImageAttribute(string html, string attributeName)
    {
        var match = LandingImageRegex().Match(html);
        if (!match.Success) return null;
        return Decode(ReadAttribute(match.Value, attributeName));
    }

    private static string? ReadDynamicImage(string html)
    {
        var match = DynamicImageRegex().Match(html);
        if (!match.Success) return null;

        var jsonish = WebUtility.HtmlDecode(match.Groups["value"].Value)
            .Replace("\\/", "/");
        return ReadFirstImageUrl(jsonish);
    }

    private static string? ReadAmazonImageFromPageJson(string html)
    {
        var decoded = WebUtility.HtmlDecode(html).Replace("\\/", "/");
        var candidates = new[] { "hiRes", "large", "mainUrl", "landingImage", "imageUrl" };

        foreach (var key in candidates)
        {
            var pattern = $"[\\\"']{Regex.Escape(key)}[\\\"']\\s*:\\s*[\\\"'](?<url>https://[^\\\"']+)[\\\"']";
            var match = Regex.Match(
                decoded,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (match.Success)
                return match.Groups["url"].Value;
        }

        return ReadFirstImageUrl(decoded);
    }

    private static string? ReadFirstImageUrl(string value)
    {
        var match = Regex.Match(
            value,
            "https://[^\\\"'<>\\s]+?\\.(?:jpg|jpeg|png|webp)(?:\\?[^\\\"'<>\\s]*)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Value : null;
    }

    private static decimal? ReadPrice(string html)
    {
        var candidates = new[]
        {
            ReadMeta(html, "property", "product:price:amount"),
            ReadMeta(html, "name", "price"),
            ReadItemPropPrice(html),
            ReadAmazonPrice(html)
        };

        foreach (var candidate in candidates)
        {
            if (TryParsePrice(candidate, out var price)) return price;
        }

        return null;
    }

    private static string? ReadItemPropPrice(string html)
    {
        var match = ItemPropPriceRegex().Match(html);
        return match.Success ? ReadAttribute(match.Value, "content") : null;
    }

    private static string? ReadAmazonPrice(string html)
    {
        var offscreen = PriceOffscreenRegex().Match(html);
        if (offscreen.Success) return Decode(StripTags(offscreen.Groups["value"].Value));

        var whole = PriceWholeRegex().Match(html);
        if (!whole.Success) return null;
        var fraction = PriceFractionRegex().Match(html, whole.Index + whole.Length);
        return fraction.Success
            ? $"{StripTags(whole.Groups["value"].Value)}.{StripTags(fraction.Groups["value"].Value)}"
            : StripTags(whole.Groups["value"].Value);
    }

    private static bool TryParsePrice(string? value, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var cleaned = Regex.Replace(value, @"[^0-9.,]", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return false;

        if (cleaned.Contains(',') && cleaned.Contains('.'))
            cleaned = cleaned.Replace(",", string.Empty);
        else if (cleaned.Count(c => c == ',') == 1 && !cleaned.Contains('.'))
            cleaned = cleaned.Replace(',', '.');
        else
            cleaned = cleaned.Replace(",", string.Empty);

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }

    private static string? CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var cleaned = Regex.Replace(title, @"\s+", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s*[:|–-]\s*Amazon(?:\.co\.uk)?\s*$", string.Empty, RegexOptions.IgnoreCase);
        return cleaned.Length > 200 ? cleaned[..200].Trim() : cleaned;
    }

    private static string? NormaliseImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        var decoded = Decode(imageUrl)?.Replace("\\/", "/").Trim();
        return Uri.TryCreate(decoded, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            ? uri.ToString()
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? Decode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value).Trim();

    private static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty);

    private static ProductMetadataResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(@"<title\b[^>]*>(?<value>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("""data-a-dynamic-image\s*=\s*["'](?<value>.*?)["']""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DynamicImageRegex();

    [GeneratedRegex("""<img\b[^>]*\bid\s*=\s*["']landingImage["'][^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LandingImageRegex();

    [GeneratedRegex("""<meta\b[^>]*\bitemprop\s*=\s*["']price["'][^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ItemPropPriceRegex();

    [GeneratedRegex("""<span\b[^>]*class\s*=\s*["'][^"']*a-offscreen[^"']*["'][^>]*>(?<value>.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceOffscreenRegex();

    [GeneratedRegex("""<span\b[^>]*class\s*=\s*["'][^"']*a-price-whole[^"']*["'][^>]*>(?<value>.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceWholeRegex();

    [GeneratedRegex("""<span\b[^>]*class\s*=\s*["'][^"']*a-price-fraction[^"']*["'][^>]*>(?<value>.*?)</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PriceFractionRegex();
}
