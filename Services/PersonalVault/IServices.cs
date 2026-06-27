using FinanceManagerAspNet.DTOs;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IItemService
{
    Task<PagedResult<ItemSummaryDto>> GetPagedAsync(ItemFilterDto filter);
    Task<ItemDetailDto?> GetByIdAsync(int id);
    Task<ItemDetailDto> CreateAsync(CreateItemDto dto, IFormFile? image = null);
    Task<ItemDetailDto?> UpdateAsync(int id, UpdateItemDto dto, IFormFile? image = null);
    Task<bool> DeleteAsync(int id);
    Task<MaintenanceLogDto> AddMaintenanceLogAsync(CreateMaintenanceLogDto dto);
    Task<LoanRecordDto> AddLoanRecordAsync(CreateLoanRecordDto dto);
    Task<LoanRecordDto?> ReturnLoanAsync(int loanId);
}

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(string name, string? description, string? icon, string? colour, int? parentId);
    Task<CategoryDto?> UpdateAsync(int id, string name, string? description, string? icon, string? colour, int? parentId);
    Task<bool> DeleteAsync(int id);
}

public interface ILocationService
{
    Task<IEnumerable<LocationDto>> GetAllAsync();
    Task<LocationDto?> GetByIdAsync(int id);
    Task<LocationDto> CreateAsync(string name, string? room, string? storageUnit, string? address);
    Task<LocationDto?> UpdateAsync(int id, string name, string? room, string? storageUnit, string? address);
    Task<bool> DeleteAsync(int id);
    Task DeleteAllAsync();
}

public interface ITagService
{
    Task<IEnumerable<TagDto>> GetAllAsync();
    Task<TagDto> CreateAsync(string name, string? colour);
    Task<bool> DeleteAsync(int id);
}

public interface IImageService
{
    Task<string?> SaveItemImageAsync(IFormFile file, int itemId);
    Task DeleteImageAsync(string? imagePath);
    bool IsValidImage(IFormFile file);
}

public interface IStatsService
{
    Task<StatsDto> GetStatsAsync();
}

public interface IExportService
{
    Task<byte[]> ExportToCsvAsync(ItemFilterDto filter);
    Task<byte[]> ExportToJsonAsync(ItemFilterDto filter);
}
