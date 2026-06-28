using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using FinanceManagerAspNet.DTOs;
using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Repositories;
using FinanceManagerAspNet.Services;
using FinanceManagerAspNet.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceManagerAspNet.Controllers;

public class ItemsController(
    IItemService itemService,
    ICategoryService categoryService,
    ILocationService locationService,
    ITagService tagService,
    AppDbContext db,
    IStatsService statsService,
    IExportService exportService,
    IImageService imageService) : Controller
{

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ItemFilterDto filter)
    {
        var canEdit = CanEdit();
        if (!canEdit)
        {
            filter.LocationId = null;
        }

        var items = await itemService.GetPagedAsync(filter);
        var stats = await statsService.GetStatsAsync();
        var categories = await categoryService.GetAllAsync();
        var locations = await locationService.GetAllAsync();
        var tags = await tagService.GetAllAsync();
        var types = await db.ItemTypes.Include(t => t.Items).OrderBy(t => t.Name).ToListAsync();
        var platforms = await db.Platforms.Include(p => p.ItemType).Include(p => p.Items).OrderBy(p => p.Name).ToListAsync();

        ViewBag.Stats = stats;
        ViewBag.Categories = categories;
        ViewBag.Locations = locations;
        ViewBag.Tags = tags;
        ViewBag.Types = types.Select(Mapper.ToDto);
        ViewBag.Platforms = platforms.Select(Mapper.ToDto);
        ViewBag.SidebarCategories = categories;
        ViewBag.Filter = filter;
        ViewBag.CanEdit = canEdit;
        ViewBag.CanViewSensitive = canEdit;

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {

        var item = await itemService.GetByIdAsync(id);

        if (item is null)
            return NotFound();

        ViewBag.CanEdit = CanEdit();
        ViewBag.CanViewSensitive = CanEdit();
        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!CanEdit()) return LoginRedirect();

        await PopulateDropdownsAsync();

        return View(new CreateItemDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateItemDto dto, IFormFile? image, List<IFormFile> photos)
    {
        if (!CanEdit()) return LoginRedirect();


        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var created = await itemService.CreateAsync(dto, image);
        await AddExtraPhotosAsync(created.Id, photos);
        TempData["Success"] = $"'{created.Name}' added to your vault.";

        return RedirectToAction("Detail", new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await itemService.GetByIdAsync(id);

        if (item is null)
            return NotFound();

        await PopulateDropdownsAsync();

        var dto = new UpdateItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Brand = item.Brand,
            Model = item.Model,
            Manufacturer = item.Manufacturer,
            CaseType = item.CaseType,
            MediaFormat = item.MediaFormat,
            Instruction = item.Instruction,
            Memory = item.Memory,
            Owner = item.Owner,
            ReleaseYear = item.ReleaseYear,
            Boxed = item.Boxed,
            Sell = item.Sell,
            Tested = item.Tested,
            SerialNumber = item.SerialNumber,
            Barcode = item.Barcode,
            PurchasePrice = item.PurchasePrice,
            CurrentValue = item.CurrentValue,
            PurchaseDate = item.PurchaseDate,
            PurchasedFrom = item.PurchasedFrom,
            WarrantyInfo = item.WarrantyInfo,
            WarrantyExpiry = item.WarrantyExpiry,
            Condition = item.Condition,
            Status = item.Status,
            CustomStatus = item.CustomStatus,
            Notes = item.Notes,
            ManualUrl = item.ManualUrl,
            IsInsured = item.IsInsured,
            InsurancePolicy = item.InsurancePolicy,
            Quantity = item.Quantity,
            CategoryId = item.CategoryId,
            ItemTypeId = item.ItemTypeId,
            PlatformId = item.PlatformId,
            LocationId = item.LocationId,
            TagIds = item.Tags.Select(t => t.Id).ToList()
        };

        ViewBag.CurrentImagePath = item.ImagePath;

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateItemDto dto, IFormFile? image, List<IFormFile> photos)
    {
        if (!CanEdit()) return LoginRedirect();

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var updated = await itemService.UpdateAsync(id, dto, image);

        if (updated is null)
            return NotFound();

        await AddExtraPhotosAsync(id, photos);
        TempData["Success"] = $"'{updated.Name}' updated.";

        return RedirectToAction("Detail", new { id });
    }


    [HttpGet]
    public async Task<IActionResult> Duplicate(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await db.Items.AsNoTracking().Include(i => i.ItemTags).FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        await PopulateDropdownsAsync();
        var dto = new CreateItemDto
        {
            Name = item.Name + " copy",
            Description = item.Description, Brand = item.Brand, Model = item.Model, Manufacturer = item.Manufacturer,
            CaseType = item.CaseType, MediaFormat = item.MediaFormat, Instruction = item.Instruction, Memory = item.Memory, Owner = item.Owner,
            ReleaseYear = item.ReleaseYear, Boxed = item.Boxed, Sell = item.Sell, Tested = item.Tested, CustomStatus = item.CustomStatus,
            PurchasePrice = item.PurchasePrice, CurrentValue = item.CurrentValue, PurchaseDate = item.PurchaseDate, PurchasedFrom = item.PurchasedFrom,
            WarrantyInfo = item.WarrantyInfo, WarrantyExpiry = item.WarrantyExpiry, Condition = item.Condition, Status = item.Status,
            Notes = item.Notes, ManualUrl = item.ManualUrl, IsInsured = item.IsInsured, InsurancePolicy = item.InsurancePolicy,
            Quantity = item.Quantity, CategoryId = item.CategoryId, ItemTypeId = item.ItemTypeId, PlatformId = item.PlatformId, LocationId = item.LocationId,
            TemplateItemId = item.Id, TagIds = item.ItemTags.Select(t => t.TagId).ToList()
        };
        TempData["Success"] = "Template loaded. Change anything unique, then save it as a new item.";
        return View("Create", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPhotos(int id, List<IFormFile> photos)
    {
        if (!CanEdit()) return LoginRedirect();
        var item = await db.Items.FindAsync(id);
        if (item is null) return NotFound();

        await AddExtraPhotosAsync(id, photos);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Additional photos updated.";
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(int id, int itemId)
    {
        if (!CanEdit()) return LoginRedirect();
        var photo = await db.ItemPhotos.FirstOrDefaultAsync(p => p.Id == id && p.ItemId == itemId);
        if (photo is null) return NotFound();
        await imageService.DeleteImageAsync(photo.ImagePath);
        db.ItemPhotos.Remove(photo);
        await db.SaveChangesAsync();
        return RedirectToAction("Detail", new { id = itemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        await itemService.DeleteAsync(id);
        TempData["Success"] = "Item removed from vault.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Loan(CreateLoanRecordDto dto)
    {
        if (!CanEdit()) return LoginRedirect();

        await itemService.AddLoanRecordAsync(dto);
        TempData["Success"] = $"Item loaned to {dto.LoanedTo}.";

        return RedirectToAction("Detail", new { id = dto.ItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnLoan(int loanId, int itemId)
    {
        if (!CanEdit()) return LoginRedirect();

        await itemService.ReturnLoanAsync(loanId);
        TempData["Success"] = "Item marked as returned.";

        return RedirectToAction("Detail", new { id = itemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMaintenance(CreateMaintenanceLogDto dto)
    {
        if (!CanEdit()) return LoginRedirect();

        await itemService.AddMaintenanceLogAsync(dto);
        TempData["Success"] = "Maintenance log added.";

        return RedirectToAction("Detail", new { id = dto.ItemId });
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv([FromQuery] ItemFilterDto filter)
    {
        if (!CanEdit()) return LoginRedirect();

        var bytes = await exportService.ExportToCsvAsync(filter);

        return File(bytes, "text/csv", $"vault-export-{DateTime.Now:yyyy-MM-dd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportJson([FromQuery] ItemFilterDto filter)
    {
        if (!CanEdit()) return LoginRedirect();

        var bytes = await exportService.ExportToJsonAsync(filter);

        return File(bytes, "application/json", $"vault-export-{DateTime.Now:yyyy-MM-dd}.json");
    }



    private async Task AddExtraPhotosAsync(int itemId, List<IFormFile>? photos)
    {
        if (photos is null) return;
        foreach (var photo in photos.Where(p => p.Length > 0))
        {
            if (!imageService.IsValidImage(photo)) continue;
            var path = await imageService.SaveItemImageAsync(photo, itemId);
            if (!string.IsNullOrWhiteSpace(path))
                db.ItemPhotos.Add(new ItemPhoto { ItemId = itemId, ImagePath = path });
        }
        await db.SaveChangesAsync();
    }

    private async Task PopulateDropdownsAsync()
    {
        var cats = await categoryService.GetAllAsync();
        var locs = await locationService.GetAllAsync();
        var tags = await tagService.GetAllAsync();

        ViewBag.SidebarCategories = cats;

        ViewBag.CategorySelectList = cats.Select(c =>
            new SelectListItem(c.Name, c.Id.ToString()));

        ViewBag.LocationSelectList = locs.Select(l =>
            new SelectListItem(
                $"{l.Name}{(l.Room is not null ? $" · {l.Room}" : "")}{(l.StorageUnit is not null ? $" · {l.StorageUnit}" : "")}",
                l.Id.ToString()));

        ViewBag.Tags = tags;

        var types = await db.ItemTypes.Include(t => t.Items).OrderBy(t => t.Name).ToListAsync();
        var platforms = await db.Platforms.Include(p => p.ItemType).Include(p => p.Items).OrderBy(p => p.Name).ToListAsync();
        var templateItems = await db.Items
            .AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.ItemType)
            .Include(i => i.Platform)
            .OrderBy(i => i.Name)
            .Take(500)
            .ToListAsync();

        ViewBag.TypeSelectList = types.Select(t => new SelectListItem(t.Name, t.Id.ToString()));
        ViewBag.PlatformSelectList = platforms.Select(p => new SelectListItem($"{p.Name}{(p.ItemType is not null ? $" · {p.ItemType.Name}" : "")}", p.Id.ToString()));
        ViewBag.TemplateItems = templateItems;

        ViewBag.Conditions = Enum.GetValues<ItemCondition>()
            .Select(c => new SelectListItem(c.ToDisplayName(), c.ToString()));

        ViewBag.Statuses = Enum.GetValues<ItemStatus>()
            .Select(s => new SelectListItem(s.ToDisplayName(), s.ToString()));
    }


    private bool CanEdit() => User.Identity?.IsAuthenticated == true;

    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
