namespace MoveInPlanner.Services.ProductMetadata;

public sealed class ProductImageRequestPolicy(IEnumerable<IRetailerProductMetadataProvider> providers)
{
    private readonly IReadOnlyList<IRetailerProductMetadataProvider> _providers =
        providers.ToList();

    public bool TryCreate(string? value, out Uri imageUri, out Uri referer)
    {
        imageUri = null!;
        referer = null!;

        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        foreach (var provider in _providers)
        {
            var providerReferer = provider.GetImageReferer(candidate);

            if (providerReferer is null) 
                continue;

            imageUri = candidate;
            referer = providerReferer;
            return true;
        }

        return false;
    }
}
