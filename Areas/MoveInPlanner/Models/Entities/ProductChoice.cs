using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Models.Entities;

public class ProductChoice
{
    public int Id { get; set; }
    public int HouseholdItemId { get; set; }
    public HouseholdItem HouseholdItem { get; set; } = null!;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public ProductTier Tier { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    [Url, StringLength(2000)]
    public string? ProductUrl { get; set; }

    [Url, StringLength(2000)]
    public string? ImageUrl { get; set; }

    [StringLength(100)]
    public string? Retailer { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool IsPreferred { get; set; }
    public bool IsPurchased { get; set; }
    public DateTime? PriceCheckedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
