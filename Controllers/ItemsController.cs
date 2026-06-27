using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using FinanceManagerAspNet.DTOs;
using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Repositories;
using FinanceManagerAspNet.Services;

namespace FinanceManagerAspNet.Controllers;

public class ItemsController(
    IItemService itemService,
    ICategoryService categoryService,
    ILocationService locationService,
    ITagService tagService,
    IStatsService statsService,
    IExportService exportService) : Controller
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

        ViewBag.Stats = stats;
        ViewBag.Categories = categories;
        ViewBag.Locations = locations;
        ViewBag.Tags = tags;
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
    public async Task<IActionResult> Create(CreateItemDto dto, IFormFile? image)
    {
        if (!CanEdit()) return LoginRedirect();


        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var created = await itemService.CreateAsync(dto, image);
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
            Notes = item.Notes,
            ManualUrl = item.ManualUrl,
            IsInsured = item.IsInsured,
            InsurancePolicy = item.InsurancePolicy,
            Quantity = item.Quantity,
            CategoryId = item.CategoryId,
            LocationId = item.LocationId,
            TagIds = item.Tags.Select(t => t.Id).ToList()
        };

        ViewBag.CurrentImagePath = item.ImagePath;

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateItemDto dto, IFormFile? image)
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

        TempData["Success"] = $"'{updated.Name}' updated.";

        return RedirectToAction("Detail", new { id });
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

        ViewBag.Conditions = Enum.GetValues<ItemCondition>()
            .Select(c => new SelectListItem(c.ToDisplayName(), c.ToString()));

        ViewBag.Statuses = Enum.GetValues<ItemStatus>()
            .Select(s => new SelectListItem(s.ToDisplayName(), s.ToString()));
    }


    private bool CanEdit() => User.Identity?.IsAuthenticated == true;

    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
