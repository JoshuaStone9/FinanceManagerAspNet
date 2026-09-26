namespace MoveInPlanner.Services.ProductMetadata;

public static class RetailerUrlMatcher
{
    public static bool UsesHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    public static bool HostMatches(string host, params string[] allowedDomains)
    {
        var value = host.TrimEnd('.').ToLowerInvariant();

        return allowedDomains.Any(domain => value == domain || value.EndsWith($".{domain}", StringComparison.Ordinal));
    }
}
