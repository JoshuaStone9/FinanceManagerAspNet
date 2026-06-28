using Microsoft.EntityFrameworkCore;
using FinanceManagerAspNet.Data;
using FinanceManagerAspNet.DTOs;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Repositories;

public class ItemRepository(AppDbContext db) : IItemRepository
{
    public async Task<PagedResult<Item>> GetPagedAsync(ItemFilterDto filter)
    {
        var query = db.Items
            .Include(i => i.Category)
            .Include(i => i.ItemType)
            .Include(i => i.Platform)
            .Include(i => i.Location)
            .Include(i => i.ItemTags).ThenInclude(it => it.Tag)
            .AsQueryable();

        // ── Filters ──────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(s) ||
                (i.Brand != null && i.Brand.ToLower().Contains(s)) ||
                (i.Model != null && i.Model.ToLower().Contains(s)) ||
                (i.Manufacturer != null && i.Manufacturer.ToLower().Contains(s)) ||
                (i.Owner != null && i.Owner.ToLower().Contains(s)) ||
                (i.CaseType != null && i.CaseType.ToLower().Contains(s)) ||
                (i.MediaFormat != null && i.MediaFormat.ToLower().Contains(s)) ||
                (i.Description != null && i.Description.ToLower().Contains(s)) ||
                (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(s)) ||
                (i.Barcode != null && i.Barcode.ToLower().Contains(s))
            );
        }


        if (filter.CategoryId.HasValue)
            query = query.Where(i => i.CategoryId == filter.CategoryId);

        if (filter.LocationId.HasValue)
            query = query.Where(i => i.LocationId == filter.LocationId);

        if (filter.ItemTypeId.HasValue)
            query = query.Where(i => i.ItemTypeId == filter.ItemTypeId);

        if (filter.PlatformId.HasValue)
            query = query.Where(i => i.PlatformId == filter.PlatformId);

        if (filter.Status.HasValue)
            query = query.Where(i => i.Status == filter.Status);

        if (filter.Condition.HasValue)
            query = query.Where(i => i.Condition == filter.Condition);

        if (filter.IsInsured.HasValue)
            query = query.Where(i => i.IsInsured == filter.IsInsured);

        if (filter.MinValue.HasValue)
            query = query.Where(i => i.CurrentValue >= filter.MinValue);

        if (filter.MaxValue.HasValue)
            query = query.Where(i => i.CurrentValue <= filter.MaxValue);

        if (filter.TagIds.Count > 0)
            query = query.Where(i => i.ItemTags.Any(it => filter.TagIds.Contains(it.TagId)));

        // ── Sort ──────────────────────────────────────────────────────────────
        query = (filter.SortBy, filter.SortDescending) switch
        {
            ("Name",        false) => query.OrderBy(i => i.Name),
            ("Name",        true)  => query.OrderByDescending(i => i.Name),
            ("Value",       false) => query.OrderBy(i => i.CurrentValue),
            ("Value",       true)  => query.OrderByDescending(i => i.CurrentValue),
            ("PurchaseDate",false) => query.OrderBy(i => i.PurchaseDate),
            ("PurchaseDate",true)  => query.OrderByDescending(i => i.PurchaseDate),
            ("CreatedAt",   false) => query.OrderBy(i => i.CreatedAt),
            ("CreatedAt",   true)  => query.OrderByDescending(i => i.CreatedAt),
            _                      => query.OrderByDescending(i => i.UpdatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<Item>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<Item?> GetByIdAsync(int id) =>
        await db.Items.FindAsync(id);

    public async Task<Item?> GetByIdWithDetailsAsync(int id) =>
        await db.Items
            .Include(i => i.Category)
            .Include(i => i.ItemType)
            .Include(i => i.Platform)
            .Include(i => i.Location)
            .Include(i => i.ItemTags).ThenInclude(it => it.Tag)
            .Include(i => i.MaintenanceLogs)
            .Include(i => i.LoanRecords)
            .Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Item> CreateAsync(Item item)
    {
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<Item> UpdateAsync(Item item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        db.Items.Update(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task DeleteAsync(int id)
    {
        var item = await db.Items.FindAsync(id);
        if (item is not null)
        {
            db.Items.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id) =>
        await db.Items.AnyAsync(i => i.Id == id);

    public async Task<IEnumerable<Item>> GetByStatusAsync(ItemStatus status) =>
        await db.Items.Where(i => i.Status == status).ToListAsync();

    public async Task<IEnumerable<Item>> SearchAsync(string query) =>
        await db.Items
            .Where(i => i.Name.Contains(query) || (i.Brand != null && i.Brand.Contains(query)))
            .Take(10)
            .ToListAsync();

    public async Task<int> GetTotalCountAsync() =>
        await db.Items.CountAsync();

    public async Task<decimal> GetTotalValueAsync() =>
        await db.Items.SumAsync(i => i.CurrentValue ?? 0);
}

public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public async Task<IEnumerable<Category>> GetAllAsync() =>
        await db.Categories
            .Include(c => c.Items)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<Category?> GetByIdAsync(int id) =>
        await db.Categories.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Category> CreateAsync(Category category)
    {
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task<Category> UpdateAsync(Category category)
    {
        db.Categories.Update(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task DeleteAsync(int id)
    {
        var cat = await db.Categories.FindAsync(id);
        if (cat is not null) { db.Categories.Remove(cat); await db.SaveChangesAsync(); }
    }

    public async Task<bool> ExistsAsync(int id) =>
        await db.Categories.AnyAsync(c => c.Id == id);
}

public class LocationRepository(AppDbContext db) : ILocationRepository
{
    public async Task<IEnumerable<Location>> GetAllAsync() =>
        await db.Locations.Include(l => l.Items).OrderBy(l => l.Name).ToListAsync();

    public async Task<Location?> GetByIdAsync(int id) =>
        await db.Locations.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<Location> CreateAsync(Location location)
    {
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location;
    }

    public async Task<Location> UpdateAsync(Location location)
    {
        db.Locations.Update(location);
        await db.SaveChangesAsync();
        return location;
    }

    public async Task DeleteAsync(int id)
    {
        var loc = await db.Locations.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id);
        if (loc is null) return;

        foreach (var item in loc.Items)
            item.LocationId = null;

        db.Locations.Remove(loc);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        var locations = await db.Locations.Include(l => l.Items).ToListAsync();
        if (locations.Count == 0) return;

        foreach (var location in locations)
        {
            foreach (var item in location.Items)
                item.LocationId = null;
        }

        db.Locations.RemoveRange(locations);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id) =>
        await db.Locations.AnyAsync(l => l.Id == id);
}

public class TagRepository(AppDbContext db) : ITagRepository
{
    public async Task<IEnumerable<Tag>> GetAllAsync() =>
        await db.Tags.OrderBy(t => t.Name).ToListAsync();

    public async Task<Tag?> GetByIdAsync(int id) =>
        await db.Tags.FindAsync(id);

    public async Task<Tag> CreateAsync(Tag tag)
    {
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    public async Task<Tag> UpdateAsync(Tag tag)
    {
        db.Tags.Update(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    public async Task DeleteAsync(int id)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag is not null) { db.Tags.Remove(tag); await db.SaveChangesAsync(); }
    }
}
