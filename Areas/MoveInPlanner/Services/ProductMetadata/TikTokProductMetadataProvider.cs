using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace MoveInPlanner.Services.ProductMetadata;

public sealed class TikTokProductMetadataProvider : IRetailerProductMetadataProvider
{
    private static readonly Uri ImageReferer = new("https://shop.tiktok.com/");

    public string RetailerName => "TikTok Shop";

    public bool Supports(Uri uri) => AllowsPageUri(uri);

    public bool AllowsPageUri(Uri uri) => RetailerUrlMatcher.UsesHttp(uri) && RetailerUrlMatcher.HostMatches(uri.Host, "tiktok.com");

    public Uri? GetImageReferer(Uri imageUri) => RetailerUrlMatcher.HostMatches(
            imageUri.Host,
            "tiktokcdn.com",
            "tiktokcdn-eu.com",
            "tiktokcdn-us.com")
            ? ImageReferer
            : null;

    public ExtractedProductMetadata Enrich(ProductMetadataPage page, ExtractedProductMetadata metadata)
    {
        foreach (var uri in new[] { page.RequestedUri }.Concat(page.Redirects))
        {
            if (!TryReadOpenGraphInfo(uri, out var name, out var imageUrl))
            {
                continue;
            }

            return metadata with
            {
                Name = ProductMetadataHtmlReader.CleanTitle(name) ?? metadata.Name,
                ImageUrl = ProductMetadataHtmlReader.NormaliseImageUrl(imageUrl) ??
                           metadata.ImageUrl,
                Price = IsProductPage(page.ResolvedUri) ? metadata.Price : null,
                ResolvedUri = CreateCanonicalProductUri(uri)
            };
        }

        return metadata;
    }

    private static bool TryReadOpenGraphInfo(Uri uri, out string? name, out string? imageUrl)
    {
        name = null;
        imageUrl = null;

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("og_info", out var value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value.ToString());
            var root = document.RootElement;

            if (root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
            {
                name = title.GetString();
            }

            if (root.TryGetProperty("image", out var image) &&
                image.ValueKind == JsonValueKind.String)
            {
                imageUrl = image.GetString();
            }

            return !string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(imageUrl);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Uri? CreateCanonicalProductUri(Uri uri)
    {
        if (!IsProductPage(uri))
        {
            return null;
        }

        return new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }

    private static bool IsProductPage(Uri uri) => uri.AbsolutePath.Contains("/pdp/", StringComparison.OrdinalIgnoreCase);
}
