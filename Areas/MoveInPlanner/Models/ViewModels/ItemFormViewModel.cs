using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Models.ViewModels;

public class ItemFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Choice type")]
    public ItemChoiceType ChoiceType { get; set; }

    public ItemPriority Priority { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Researching;

    [Range(1, 999), Display(Name = "Quantity required")]
    public int QuantityRequired { get; set; } = 1;

    [Range(0, 1000000), Display(Name = "Target budget")]
    public decimal? TargetBudget { get; set; }

    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "The total purchase price must be between £0 and £1,000,000.")]
    [Display(Name = "Total purchase price")]
    public decimal? PurchasedPrice { get; set; }

    [Display(Name = "Essential for move-in")]
    public bool IsEssentialForMoveIn { get; set; }

    [DataType(DataType.Date), Display(Name = "Needed by")]
    public DateTime? NeededBy { get; set; }

    [StringLength(2000), Display(Name = "General notes")]
    public string? GeneralNotes { get; set; }

    [StringLength(1000), Display(Name = "Decision reason")]
    public string? DecisionReason { get; set; }

    [Display(Name = "Full comparison response")]
    public string? FullComparisonResponse { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = [];
}
