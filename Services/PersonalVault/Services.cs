using Microsoft.EntityFrameworkCore;
using FinanceManagerAspNet.Data;
using FinanceManagerAspNet.DTOs;
using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Repositories;
using System.Text;
using System.Text.Json;

namespace FinanceManagerAspNet.Services;

// ─── Mapping helpers ──────────────────────────────────────────────────────────

public static class Mapper
{
    public static ItemSummaryDto ToSummary(Item i) => new(
        i.Id, i.Name, i.Brand, i.Model, i.Manufacturer,
        i.Category?.Name, i.Category?.Colour, i.Category?.Icon,
        i.ItemType?.Name, i.Platform?.Name,
        i.Location is null ? null : $"{i.Location.Name}{(i.Location.Room is not null ? $" · {i.Location.Room}" : "")}",
        i.CurrentValue, i.Condition, i.Status, i.CustomStatus, i.ImagePath, i.Quantity, i.UpdatedAt,
        i.ItemTags.Select(it => it.Tag!.Name)
    );

    public static ItemDetailDto ToDetail(Item i) => new(
        i.Id, i.Name, i.Description, i.Brand, i.Model,
        i.Manufacturer, i.CaseType, i.MediaFormat, i.Instruction, i.Memory, i.Owner, i.ReleaseYear, i.Boxed, i.Sell, i.Tested,
        i.SerialNumber, i.Barcode, i.PurchasePrice, i.CurrentValue,
        i.PurchaseDate, i.PurchasedFrom, i.WarrantyInfo, i.WarrantyExpiry,
        i.Condition, i.Status, i.CustomStatus, i.Notes, i.ImagePath, i.ManualUrl, i.ReceiptImagePath,
        i.Photos.Select(p => new ItemPhotoDto(p.Id, p.ImagePath, p.Caption, p.CreatedAt)),
        i.IsInsured, i.InsurancePolicy, i.Quantity, i.CreatedAt, i.UpdatedAt,
        i.CategoryId, i.Category?.Name, i.ItemTypeId, i.ItemType?.Name, i.PlatformId, i.Platform?.Name, i.LocationId, i.Location?.Name, i.Location?.Room,
        i.ItemTags.Select(it => new TagDto(it.Tag!.Id, it.Tag.Name, it.Tag.Colour)),
        i.MaintenanceLogs.Select(m => new MaintenanceLogDto(m.Id, m.Description, m.Cost, m.Date, m.NextDueDate, m.PerformedBy)),
        i.LoanRecords.Select(l => new LoanRecordDto(l.Id, l.LoanedTo, l.ContactInfo, l.LoanedOn, l.DueBack, l.ReturnedOn, l.IsReturned, l.Notes))
    );

    public static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Description, c.Icon, c.Colour, c.ParentCategoryId, c.Items.Count);
    public static LocationDto ToDto(Location l) => new(l.Id, l.Name, l.Room, l.StorageUnit, l.Address, l.Items.Count);
    public static ItemTypeDto ToDto(ItemType t) => new(t.Id, t.Name, t.Description, t.Items.Count);
    public static PlatformDto ToDto(Platform p) => new(p.Id, p.Name, p.Description, p.ItemTypeId, p.ItemType?.Name, p.Items.Count);
    public static TagDto ToDto(Tag t) => new(t.Id, t.Name, t.Colour);
}

// ─── ItemService ──────────────────────────────────────────────────────────────

public class ItemService(IItemRepository items, AppDbContext db, IImageService images) : IItemService
{
    public async Task<PagedResult<ItemSummaryDto>> GetPagedAsync(ItemFilterDto filter)
    {
        var paged = await items.GetPagedAsync(filter);
        return new PagedResult<ItemSummaryDto>(
            paged.Items.Select(Mapper.ToSummary),
            paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<ItemDetailDto?> GetByIdAsync(int id)
    {
        var item = await items.GetByIdWithDetailsAsync(id);
        return item is null ? null : Mapper.ToDetail(item);
    }

    public async Task<ItemDetailDto> CreateAsync(CreateItemDto dto, IFormFile? image = null)
    {
        var item = new Item
        {
            Name = dto.Name,
            Description = dto.Description,
            Brand = dto.Brand,
            Model = dto.Model,
            Manufacturer = dto.Manufacturer,
            CaseType = dto.CaseType,
            MediaFormat = dto.MediaFormat,
            Instruction = dto.Instruction,
            Memory = dto.Memory,
            Owner = dto.Owner,
            ReleaseYear = dto.ReleaseYear,
            Boxed = dto.Boxed,
            Sell = dto.Sell,
            Tested = dto.Tested,
            CustomStatus = Clean(dto.CustomStatus),
            SerialNumber = dto.SerialNumber,
            Barcode = dto.Barcode,
            PurchasePrice = dto.PurchasePrice,
            CurrentValue = dto.CurrentValue ?? dto.PurchasePrice,
            PurchaseDate = dto.PurchaseDate,
            PurchasedFrom = dto.PurchasedFrom,
            WarrantyInfo = dto.WarrantyInfo,
            WarrantyExpiry = dto.WarrantyExpiry,
            Condition = dto.Condition,
            Status = dto.Status,
            Notes = dto.Notes,
            ManualUrl = dto.ManualUrl,
            IsInsured = dto.IsInsured,
            InsurancePolicy = dto.InsurancePolicy,
            Quantity = dto.Quantity,
            CategoryId = dto.CategoryId,
            ItemTypeId = dto.ItemTypeId,
            PlatformId = dto.PlatformId,
            LocationId = await ResolveLocationIdAsync(dto)
        };

        var created = await items.CreateAsync(item);

        if (image is not null && images.IsValidImage(image))
            created.ImagePath = await images.SaveItemImageAsync(image, created.Id);

        if (dto.TagIds.Count > 0)
        {
            var itemTags = dto.TagIds.Select(tid => new ItemTag { ItemId = created.Id, TagId = tid });
            db.ItemTags.AddRange(itemTags);
        }

        await db.SaveChangesAsync();
        var full = await items.GetByIdWithDetailsAsync(created.Id);
        return Mapper.ToDetail(full!);
    }

    public async Task<ItemDetailDto?> UpdateAsync(int id, UpdateItemDto dto, IFormFile? image = null)
    {
        var item = await items.GetByIdWithDetailsAsync(id);
        if (item is null) return null;

        item.Name = dto.Name;
        item.Description = dto.Description;
        item.Brand = dto.Brand;
        item.Model = dto.Model;
        item.Manufacturer = dto.Manufacturer;
        item.CaseType = dto.CaseType;
        item.MediaFormat = dto.MediaFormat;
        item.Instruction = dto.Instruction;
        item.Memory = dto.Memory;
        item.Owner = dto.Owner;
        item.ReleaseYear = dto.ReleaseYear;
        item.Boxed = dto.Boxed;
        item.Sell = dto.Sell;
        item.Tested = dto.Tested;
        item.CustomStatus = Clean(dto.CustomStatus);
        item.SerialNumber = dto.SerialNumber;
        item.Barcode = dto.Barcode;
        item.PurchasePrice = dto.PurchasePrice;
        item.CurrentValue = dto.CurrentValue;
        item.PurchaseDate = dto.PurchaseDate;
        item.PurchasedFrom = dto.PurchasedFrom;
        item.WarrantyInfo = dto.WarrantyInfo;
        item.WarrantyExpiry = dto.WarrantyExpiry;
        item.Condition = dto.Condition;
        item.Status = dto.Status;
        item.Notes = dto.Notes;
        item.ManualUrl = dto.ManualUrl;
        item.IsInsured = dto.IsInsured;
        item.InsurancePolicy = dto.InsurancePolicy;
        item.Quantity = dto.Quantity;
        item.CategoryId = dto.CategoryId;
        item.ItemTypeId = dto.ItemTypeId;
        item.PlatformId = dto.PlatformId;
        item.LocationId = await ResolveLocationIdAsync(dto);

        if (image is not null && images.IsValidImage(image))
        {
            await images.DeleteImageAsync(item.ImagePath);
            item.ImagePath = await images.SaveItemImageAsync(image, id);
        }

        // Sync tags
        db.ItemTags.RemoveRange(item.ItemTags);
        if (dto.TagIds.Count > 0)
            db.ItemTags.AddRange(dto.TagIds.Select(tid => new ItemTag { ItemId = id, TagId = tid }));

        await items.UpdateAsync(item);
        var full = await items.GetByIdWithDetailsAsync(id);
        return Mapper.ToDetail(full!);
    }

    private async Task<int?> ResolveLocationIdAsync(CreateItemDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.NewLocationName))
        {
            var location = new Location
            {
                Name = dto.NewLocationName.Trim(),
                Room = Clean(dto.NewLocationRoom),
                StorageUnit = Clean(dto.NewLocationStorageUnit),
                Address = Clean(dto.NewLocationAddress)
            };
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            return location.Id;
        }

        return dto.LocationId;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<bool> DeleteAsync(int id)
    {
        if (!await items.ExistsAsync(id)) return false;
        var item = await db.Items.Include(i => i.Photos).FirstOrDefaultAsync(i => i.Id == id);
        await images.DeleteImageAsync(item?.ImagePath);
        if (item is not null)
        {
            foreach (var photo in item.Photos)
                await images.DeleteImageAsync(photo.ImagePath);
        }
        await items.DeleteAsync(id);
        return true;
    }

    public async Task<MaintenanceLogDto> AddMaintenanceLogAsync(CreateMaintenanceLogDto dto)
    {
        var log = new MaintenanceLog
        {
            ItemId = dto.ItemId,
            Description = dto.Description,
            Cost = dto.Cost,
            Date = dto.Date,
            NextDueDate = dto.NextDueDate,
            PerformedBy = dto.PerformedBy
        };
        db.MaintenanceLogs.Add(log);
        await db.SaveChangesAsync();
        return new MaintenanceLogDto(log.Id, log.Description, log.Cost, log.Date, log.NextDueDate, log.PerformedBy);
    }

    public async Task<LoanRecordDto> AddLoanRecordAsync(CreateLoanRecordDto dto)
    {
        var loan = new LoanRecord
        {
            ItemId = dto.ItemId,
            LoanedTo = dto.LoanedTo,
            ContactInfo = dto.ContactInfo,
            DueBack = dto.DueBack,
            Notes = dto.Notes
        };
        db.LoanRecords.Add(loan);

        // Update item status
        var item = await db.Items.FindAsync(dto.ItemId);
        if (item is not null) item.Status = ItemStatus.OnLoan;

        await db.SaveChangesAsync();
        return new LoanRecordDto(loan.Id, loan.LoanedTo, loan.ContactInfo, loan.LoanedOn, loan.DueBack, null, false, loan.Notes);
    }

    public async Task<LoanRecordDto?> ReturnLoanAsync(int loanId)
    {
        var loan = await db.LoanRecords.Include(l => l.Item).FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan is null) return null;
        loan.ReturnedOn = DateTime.UtcNow;
        if (loan.Item is not null) loan.Item.Status = ItemStatus.EasilyAccessible;
        await db.SaveChangesAsync();
        return new LoanRecordDto(loan.Id, loan.LoanedTo, loan.ContactInfo, loan.LoanedOn, loan.DueBack, loan.ReturnedOn, true, loan.Notes);
    }
}

// ─── CategoryService ──────────────────────────────────────────────────────────

public class CategoryService(ICategoryRepository repo) : ICategoryService
{
    public async Task<IEnumerable<CategoryDto>> GetAllAsync() =>
        (await repo.GetAllAsync()).Select(Mapper.ToDto);

    public async Task<CategoryDto?> GetByIdAsync(int id) =>
        (await repo.GetByIdAsync(id)) is { } c ? Mapper.ToDto(c) : null;

    public async Task<CategoryDto> CreateAsync(string name, string? description, string? icon, string? colour, int? parentId)
    {
        var cat = new Category { Name = name, Description = description, Icon = icon, Colour = colour, ParentCategoryId = parentId };
        return Mapper.ToDto(await repo.CreateAsync(cat));
    }

    public async Task<CategoryDto?> UpdateAsync(int id, string name, string? description, string? icon, string? colour, int? parentId)
    {
        var cat = await repo.GetByIdAsync(id);
        if (cat is null) return null;
        cat.Name = name; cat.Description = description; cat.Icon = icon; cat.Colour = colour; cat.ParentCategoryId = parentId;
        return Mapper.ToDto(await repo.UpdateAsync(cat));
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (!await repo.ExistsAsync(id)) return false;
        await repo.DeleteAsync(id);
        return true;
    }
}

// ─── LocationService ──────────────────────────────────────────────────────────

public class LocationService(ILocationRepository repo) : ILocationService
{
    public async Task<IEnumerable<LocationDto>> GetAllAsync() =>
        (await repo.GetAllAsync()).Select(Mapper.ToDto);

    public async Task<LocationDto?> GetByIdAsync(int id) =>
        (await repo.GetByIdAsync(id)) is { } l ? Mapper.ToDto(l) : null;

    public async Task<LocationDto> CreateAsync(string name, string? room, string? storageUnit, string? address)
    {
        var loc = new Location { Name = name, Room = room, StorageUnit = storageUnit, Address = address };
        return Mapper.ToDto(await repo.CreateAsync(loc));
    }

    public async Task<LocationDto?> UpdateAsync(int id, string name, string? room, string? storageUnit, string? address)
    {
        var loc = await repo.GetByIdAsync(id);
        if (loc is null) return null;
        loc.Name = name; loc.Room = room; loc.StorageUnit = storageUnit; loc.Address = address;
        return Mapper.ToDto(await repo.UpdateAsync(loc));
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (!await repo.ExistsAsync(id)) return false;
        await repo.DeleteAsync(id);
        return true;
    }

    public async Task DeleteAllAsync() => await repo.DeleteAllAsync();
}

// ─── TagService ───────────────────────────────────────────────────────────────

public class TagService(ITagRepository repo) : ITagService
{
    public async Task<IEnumerable<TagDto>> GetAllAsync() =>
        (await repo.GetAllAsync()).Select(Mapper.ToDto);

    public async Task<TagDto> CreateAsync(string name, string? colour) =>
        Mapper.ToDto(await repo.CreateAsync(new Tag { Name = name, Colour = colour }));

    public async Task<bool> DeleteAsync(int id)
    {
        var tag = await repo.GetByIdAsync(id);
        if (tag is null) return false;
        await repo.DeleteAsync(id);
        return true;
    }
}

// ─── ImageService ─────────────────────────────────────────────────────────────

public class ImageService(IConfiguration config, IWebHostEnvironment env, ILogger<ImageService> logger) : IImageService
{
    private static readonly string[] AllowedExts = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxSize = 5 * 1024 * 1024; // 5 MB

    public bool IsValidImage(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLower();
        return AllowedExts.Contains(ext) && file.Length <= MaxSize && file.Length > 0;
    }

    public async Task<string?> SaveItemImageAsync(IFormFile file, int itemId)
    {
        try
        {
            var folder = Path.Combine(env.WebRootPath, "images", "items");
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"item_{itemId}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, fileName);
            await using var stream = File.Create(fullPath);
            await file.CopyToAsync(stream);
            return $"/images/items/{fileName}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save image for item {ItemId}", itemId);
            return null;
        }
    }

    public async Task DeleteImageAsync(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;
        var full = Path.Combine(env.WebRootPath, imagePath.TrimStart('/'));
        if (File.Exists(full)) File.Delete(full);
        await Task.CompletedTask;
    }
}

// ─── StatsService ─────────────────────────────────────────────────────────────

public class StatsService(AppDbContext db) : IStatsService
{
    public async Task<StatsDto> GetStatsAsync()
    {
        var total = await db.Items.CountAsync();
        var categories = await db.Categories.CountAsync();
        // SQL Server-safe aggregate.
        var totalValueDouble = await db.Items.SumAsync(i => (double?)(i.CurrentValue) ?? 0.0);
        var totalValue = (decimal)totalValueDouble;
        var activeLoans = await db.LoanRecords.CountAsync(l => l.ReturnedOn == null);
        var expiringWarranties = await db.Items.CountAsync(i =>
            i.WarrantyExpiry.HasValue && i.WarrantyExpiry > DateTime.UtcNow && i.WarrantyExpiry < DateTime.UtcNow.AddDays(30));
        var maintenanceDue = await db.MaintenanceLogs.CountAsync(m =>
            m.NextDueDate.HasValue && m.NextDueDate < DateTime.UtcNow.AddDays(7));

        var byStatus = await db.Items
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count);

        var byCategory = await db.Items
            .Where(i => i.Category != null)
            .GroupBy(i => i.Category!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Name, g => g.Count);

        return new StatsDto(total, categories, totalValue, activeLoans, expiringWarranties, maintenanceDue, byStatus, byCategory);
    }
}

// ─── ExportService ────────────────────────────────────────────────────────────

public class ExportService(IItemRepository items) : IExportService
{
    public async Task<byte[]> ExportToCsvAsync(ItemFilterDto filter)
    {
        filter.PageSize = 10000;
        var paged = await items.GetPagedAsync(filter);
        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Brand,Model,Category,Location,Status,Condition,PurchasePrice,CurrentValue,PurchaseDate,SerialNumber,Notes");
        foreach (var i in paged.Items)
            sb.AppendLine($"{i.Id},\"{i.Name}\",\"{i.Brand}\",\"{i.Model}\",\"{i.Category?.Name}\",\"{i.Location?.Name}\",{i.Status},{i.Condition},{i.PurchasePrice},{i.CurrentValue},{i.PurchaseDate:yyyy-MM-dd},\"{i.SerialNumber}\",\"{i.Notes}\"");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportToJsonAsync(ItemFilterDto filter)
    {
        filter.PageSize = 10000;
        var paged = await items.GetPagedAsync(filter);
        var dtos = paged.Items.Select(Mapper.ToSummary);
        return JsonSerializer.SerializeToUtf8Bytes(dtos, new JsonSerializerOptions { WriteIndented = true });
    }
}
