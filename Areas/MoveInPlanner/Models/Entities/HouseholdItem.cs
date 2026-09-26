using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Models.Entities;

public class HouseholdItem
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ItemChoiceType ChoiceType { get; set; }
    public ItemPriority Priority { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Researching;

    [Range(1, 999)]
    public int QuantityRequired { get; set; } = 1;

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TargetBudget { get; set; }

    public bool IsEssentialForMoveIn { get; set; }
    public DateTime? NeededBy { get; set; }

    [StringLength(2000)]
    public string? GeneralNotes { get; set; }

    [StringLength(1000)]
    public string? DecisionReason { get; set; }

    public string? FullComparisonResponse { get; set; }

    public int? SelectedProductChoiceId { get; set; }
    public ProductChoice? SelectedProductChoice { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualPurchasePrice { get; set; }

    public DateTime? PurchasedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ProductChoice> ProductChoices { get; set; } = new List<ProductChoice>();
}
