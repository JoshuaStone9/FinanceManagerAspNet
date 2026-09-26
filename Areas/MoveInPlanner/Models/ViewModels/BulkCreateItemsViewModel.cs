using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MoveInPlanner.Models.ViewModels;

public class BulkCreateItemsViewModel
{
    [Display(Name = "Default category")]
    public int? DefaultCategoryId { get; set; }

    [Display(Name = "Mark all as purchased")]
    public bool MarkAsPurchased { get; set; } = true;

    public List<BulkCreateItemRowViewModel> Rows { get; set; } = [];

    public IEnumerable<SelectListItem> Categories { get; set; } = [];
}

public class BulkCreateItemRowViewModel
{
    [StringLength(150)]
    public string? Name { get; set; }

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Enter a price between £0.01 and £1,000,000.")]
    public decimal? Price { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    public bool IsBlank => string.IsNullOrWhiteSpace(Name) && !Price.HasValue;
}
