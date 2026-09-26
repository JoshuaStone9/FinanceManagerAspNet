using MoveInPlanner.Models.Results;

namespace MoveInPlanner.Services.ProductMetadata;

public interface IProductMetadataService
{
    Task<ProductMetadataResult> GetFromUrlAsync(
        string productUrl,
        CancellationToken cancellationToken = default);
}
