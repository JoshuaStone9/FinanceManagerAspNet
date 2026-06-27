namespace FinanceManagerAspNet.Models;

// ─── Enums ────────────────────────────────────────────────────────────────────

public enum ItemCondition
{
    None,
    New,
    LikeNew,
    Good,
    Fair,
    Poor
}

public enum ItemStatus
{
    EasyAccess,
    NearbyStorage,
    StoredAway,
    HardToReach,
    LoanedOut,
    Sold,
    Disposed,
    Missing
}

// ─── Entities ─────────────────────────────────────────────────────────────────

public class Item
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Barcode { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchasedFrom { get; set; }
    public string? WarrantyInfo { get; set; }
    public DateTime? WarrantyExpiry { get; set; }
    public ItemCondition Condition { get; set; } = ItemCondition.None;
    public ItemStatus Status { get; set; } = ItemStatus.EasyAccess;
    public string? Notes { get; set; }
    public string? ImagePath { get; set; }
    public string? ManualUrl { get; set; }
    public string? ReceiptImagePath { get; set; }
    public bool IsInsured { get; set; }
    public string? InsurancePolicy { get; set; }
    public int? Quantity { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation


    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public ICollection<ItemTag> ItemTags { get; set; } = [];
    public ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = [];
    public ICollection<LoanRecord> LoanRecords { get; set; } = [];
}

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }   // Lucide icon name
    public string? Colour { get; set; } // Hex colour for badge
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = [];
    public ICollection<Item> Items { get; set; } = [];
}

public class Location
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Room { get; set; }
    public string? StorageUnit { get; set; } // e.g. "Drawer 3", "Shelf B"
    public ICollection<Item> Items { get; set; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Colour { get; set; }
    public ICollection<ItemTag> ItemTags { get; set; } = [];
}

public class ItemTag
{
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}

public class MaintenanceLog
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public required string Description { get; set; }
    public decimal? Cost { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public DateTime? NextDueDate { get; set; }
    public string? PerformedBy { get; set; }
}

public class LoanRecord
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public required string LoanedTo { get; set; }
    public string? ContactInfo { get; set; }
    public DateTime LoanedOn { get; set; } = DateTime.UtcNow;
    public DateTime? DueBack { get; set; }
    public DateTime? ReturnedOn { get; set; }
    public string? Notes { get; set; }
    public bool IsReturned => ReturnedOn.HasValue;
}
