using System.ComponentModel.DataAnnotations;

namespace MoveInPlanner.Models.Requests;

public sealed class ProductMetadataRequest
{
    [Required, Url, StringLength(2000)]
    public string ProductUrl { get; set; } = string.Empty;
}
