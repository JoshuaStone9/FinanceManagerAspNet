namespace MoveInPlanner.Services.ProductMetadata;

public interface IRetailerProductMetadataProvider
{
    string RetailerName { get; }

    bool Supports(Uri uri);

    bool AllowsPageUri(Uri uri);

    Uri? GetImageReferer(Uri imageUri);

    ExtractedProductMetadata Enrich(ProductMetadataPage page, ExtractedProductMetadata metadata);
}

public sealed record ProductMetadataPage(Uri RequestedUri, Uri ResolvedUri, string Html, IReadOnlyList<Uri> Redirects);

public sealed record ExtractedProductMetadata(string? Name = null, decimal? Price = null, string? ImageUrl = null, Uri? ResolvedUri = null);
