using FinanceManagerAspNet.DTOs;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Repositories;

// ─── Item Repository ──────────────────────────────────────────────────────────

public interface IItemRepository
{
    Task<PagedResult<Item>> GetPagedAsync(ItemFilterDto filter);
    Task<Item?> GetByIdAsync(int id);
    Task<Item?> GetByIdWithDetailsAsync(int id);
    Task<Item> CreateAsync(Item item);
    Task<Item> UpdateAsync(Item item);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<IEnumerable<Item>> GetByStatusAsync(ItemStatus status);
    Task<IEnumerable<Item>> SearchAsync(string query);
    Task<int> GetTotalCountAsync();
    Task<decimal> GetTotalValueAsync();
}

// ─── Category Repository ──────────────────────────────────────────────────────

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllAsync();
    Task<Category?> GetByIdAsync(int id);
    Task<Category> CreateAsync(Category category);
    Task<Category> UpdateAsync(Category category);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

// ─── Location Repository ──────────────────────────────────────────────────────

public interface ILocationRepository
{
    Task<IEnumerable<Location>> GetAllAsync();
    Task<Location?> GetByIdAsync(int id);
    Task<Location> CreateAsync(Location location);
    Task<Location> UpdateAsync(Location location);
    Task DeleteAsync(int id);
    Task DeleteAllAsync();
    Task<bool> ExistsAsync(int id);
}

// ─── Tag Repository ───────────────────────────────────────────────────────────

public interface ITagRepository
{
    Task<IEnumerable<Tag>> GetAllAsync();
    Task<Tag?> GetByIdAsync(int id);
    Task<Tag> CreateAsync(Tag tag);
    Task<Tag> UpdateAsync(Tag tag);
    Task DeleteAsync(int id);
}
