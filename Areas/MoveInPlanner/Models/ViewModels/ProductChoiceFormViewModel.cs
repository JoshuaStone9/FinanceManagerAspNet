using System.ComponentModel.DataAnnotations;
using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Models.ViewModels;

public class ProductChoiceFormViewModel
{
    public int? Id { get; set; }

    [Required]
    public int HouseholdItemId { get; set; }

    public string HouseholdItemName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    public ProductTier Tier { get; set; } = ProductTier.Standard;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Range(1, 999)]
    [Display(Name = "Purchase quantity")]
    public int Quantity { get; set; } = 1;

    [Url, StringLength(2000)]
    [Display(Name = "Product URL")]
    public string? ProductUrl { get; set; }

    [Url, StringLength(2000)]
    [Display(Name = "Product image URL")]
    public string? ImageUrl { get; set; }

    [StringLength(100)]
    public string? Retailer { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Display(Name = "Include in preferred plan")]
    public bool IsPreferred { get; set; }

    [Display(Name = "Already purchased")]
    public bool IsPurchased { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Price checked on")]
    public DateTime? PriceCheckedOn { get; set; }
}
