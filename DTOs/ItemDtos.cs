using System.ComponentModel.DataAnnotations;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.DTOs;

// ─── Item DTOs ────────────────────────────────────────────────────────────────

public record ItemSummaryDto(
    int Id,
    string Name,
    string? Brand,
    string? Model,
    string? Manufacturer,
    string? CategoryName,
    string? CategoryColour,
    string? CategoryIcon,
    string? TypeName,
    string? PlatformName,
    string? LocationName,
    decimal? CurrentValue,
    ItemCondition Condition,
    ItemStatus Status,
    string? CustomStatus,
    string? ImagePath,
    int? Quantity,
    DateTime UpdatedAt,
    IEnumerable<string> Tags
);

public record ItemDetailDto(
    int Id,
    string Name,
    string? Description,
    string? Brand,
    string? Model,
    string? Manufacturer,
    string? CaseType,
    string? MediaFormat,
    string? Instruction,
    string? Memory,
    string? Owner,
    int? ReleaseYear,
    bool? Boxed,
    bool? Sell,
    string? Tested,
    string? SerialNumber,
    string? Barcode,
    decimal? PurchasePrice,
    decimal? CurrentValue,
    DateTime? PurchaseDate,
    string? PurchasedFrom,
    string? WarrantyInfo,
    DateTime? WarrantyExpiry,
    ItemCondition Condition,
    ItemStatus Status,
    string? CustomStatus,
    string? Notes,
    string? ImagePath,
    string? ManualUrl,
    string? ReceiptImagePath,
    IEnumerable<ItemPhotoDto> Photos,
    bool IsInsured,
    string? InsurancePolicy,
    int? Quantity,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? CategoryId,
    string? CategoryName,
    int? ItemTypeId,
    string? ItemTypeName,
    int? PlatformId,
    string? PlatformName,
    int? LocationId,
    string? LocationName,
    string? LocationRoom,
    IEnumerable<TagDto> Tags,
    IEnumerable<MaintenanceLogDto> MaintenanceLogs,
    IEnumerable<LoanRecordDto> LoanRecords
);

public class CreateItemDto
{

    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public string? CaseType { get; set; }
    public string? MediaFormat { get; set; }
    public string? Instruction { get; set; }
    public string? Memory { get; set; }
    public string? Owner { get; set; }
    public int? ReleaseYear { get; set; }
    public bool? Boxed { get; set; }
    public bool? Sell { get; set; }
    public string? Tested { get; set; }
    public string? CustomStatus { get; set; }
    public string? SerialNumber { get; set; }
    public string? Barcode { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchasedFrom { get; set; }
    public string? WarrantyInfo { get; set; }
    public DateTime? WarrantyExpiry { get; set; }
    public ItemCondition Condition { get; set; } = ItemCondition.None;
    public ItemStatus Status { get; set; } = ItemStatus.EasilyAccessible;
    public string? Notes { get; set; }
    public string? ManualUrl { get; set; }
    public bool IsInsured { get; set; }
    public string? InsurancePolicy { get; set; }
    public int? Quantity { get; set; } = 1;
    public int? CategoryId { get; set; }
    public int? ItemTypeId { get; set; }
    public int? PlatformId { get; set; }
    public int? LocationId { get; set; }
    public string? NewLocationName { get; set; }
    public string? NewLocationRoom { get; set; }
    public string? NewLocationStorageUnit { get; set; }
    public string? NewLocationAddress { get; set; }
    public int? TemplateItemId { get; set; }
    public List<int> TagIds { get; set; } = [];
}

public class UpdateItemDto : CreateItemDto
{
    public int Id { get; set; }
}

// ─── Filter / Search ──────────────────────────────────────────────────────────

public class ItemFilterDto
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemTypeId { get; set; }
    public int? PlatformId { get; set; }
    public int? LocationId { get; set; }
    public ItemStatus? Status { get; set; }
    public ItemCondition? Condition { get; set; }
    public List<int> TagIds { get; set; } = [];
    public bool? IsInsured { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? SortBy { get; set; } = "UpdatedAt";
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// ─── Supporting DTOs ──────────────────────────────────────────────────────────

public record CategoryDto(int Id, string Name, string? Description, string? Icon, string? Colour, int? ParentCategoryId, int ItemCount);

public record LocationDto(int Id, string Name, string? Room, string? StorageUnit, string? Address, int ItemCount);

public record ItemTypeDto(int Id, string Name, string? Description, int ItemCount);

public record PlatformDto(int Id, string Name, string? Description, int? ItemTypeId, string? ItemTypeName, int ItemCount);

public record TagDto(int Id, string Name, string? Colour);

public record ItemPhotoDto(int Id, string ImagePath, string? Caption, DateTime CreatedAt);

public record MaintenanceLogDto(int Id, string Description, decimal? Cost, DateTime Date, DateTime? NextDueDate, string? PerformedBy);

public record LoanRecordDto(int Id, string LoanedTo, string? ContactInfo, DateTime LoanedOn, DateTime? DueBack, DateTime? ReturnedOn, bool IsReturned, string? Notes);

public record StatsDto(
    int TotalItems,
    int TotalCategories,
    decimal TotalValue,
    int ActiveLoans,
    int ExpiringWarranties,
    int ItemsNeedingMaintenance,
    Dictionary<string, int> ItemsByStatus,
    Dictionary<string, int> ItemsByCategory
);

public record CreateMaintenanceLogDto(int ItemId, string Description, decimal? Cost, DateTime Date, DateTime? NextDueDate, string? PerformedBy);

public record CreateLoanRecordDto(int ItemId, string LoanedTo, string? ContactInfo, DateTime? DueBack, string? Notes);
