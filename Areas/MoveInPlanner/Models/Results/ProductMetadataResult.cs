namespace MoveInPlanner.Models.Results;

public sealed class ProductMetadataResult
{
    public bool Success { get; init; }
    public string? Name { get; init; }
    public decimal? Price { get; init; }
    public string? ImageUrl { get; init; }
    public string? Retailer { get; init; }
    public string? ResolvedUrl { get; init; }
    public string? ErrorMessage { get; init; }
}
