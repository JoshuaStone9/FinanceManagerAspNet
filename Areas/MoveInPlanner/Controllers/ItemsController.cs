using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Models.Entities;
using MoveInPlanner.Models.Enums;
using MoveInPlanner.Models.ViewModels;
using MoveInPlanner.Services;

namespace MoveInPlanner.Controllers;

[Area("MoveInPlanner")]
public class ItemsController(ApplicationDbContext db) : MoveInPlannerController
{
    public async Task<IActionResult> Index(string? search, int? categoryId, string? status, string? view)
    {
        var query = db.HouseholdItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(item => item.Name.Contains(trimmedSearch));
        }

        if (categoryId.HasValue)
            query = query.Where(item => item.CategoryId == categoryId);

        if (status == "NotPurchased")
            query = query.Where(item => item.Status != PurchaseStatus.Purchased);
        else if (Enum.TryParse<PurchaseStatus>(status, true, out var purchaseStatus)
            && Enum.IsDefined(purchaseStatus))
        {
            query = query.Where(item => item.Status == purchaseStatus);
            status = purchaseStatus.ToString();
        }
        else
            status = null;

        var items = await query
            .OrderBy(item => item.Category.Name)
            .ThenBy(item => item.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(
            await db.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync(),
            "Id",
            "Name",
            categoryId);

        var model = new ItemsIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Status = status,
            View = view is "compact" or "table" ? view : "cards",
            TotalItems = items.Count,

            PurchasedItems = items.Count(item => item.Status == PurchaseStatus.Purchased),

            CurrentPlanValue = items.Sum(HouseholdItemValueCalculator.CurrentPlanValue),

            RemainingPlanValue = items.Sum(HouseholdItemValueCalculator.RemainingPlanValue),

            PurchasedValue = items.Sum(HouseholdItemValueCalculator.PurchasedValue),

            Categories = items
                .GroupBy(item => new
                {
                    item.CategoryId,
                    item.Category.Name
                })
                .OrderBy(group => group.Key.Name)
                .Select(group => new ItemCategorySectionViewModel
                {
                    CategoryId = group.Key.CategoryId,
                    Name = group.Key.Name,
                    TotalItems = group.Count(),

                    PurchasedItems = group.Count(item => item.Status == PurchaseStatus.Purchased),

                    CurrentPlanValue = group.Sum(HouseholdItemValueCalculator.CurrentPlanValue),

                    Items = group
                        .OrderBy(item => item.Name)
                        .Select(item => new ItemListCardViewModel
                        {
                            Id = item.Id,
                            Name = item.Name,
                            GeneralNotes = item.GeneralNotes,
                            Status = item.Status,
                            Priority = item.Priority,
                            IsEssentialForMoveIn = item.IsEssentialForMoveIn,
                            QuantityRequired = item.QuantityRequired,
                            TargetBudget = item.TargetBudget,
                            PurchaseOptionCount = item.ProductChoices.Count,
                            PreferredOptionCount = item.ProductChoices.Count(choice => choice.IsPreferred),
                            PreferredPlanValue = HouseholdItemValueCalculator.PreferredPlanValue(item),
                            CheapestOptionValue = HouseholdItemValueCalculator.CheapestOptionValue(item),
                            CurrentPlanValue = HouseholdItemValueCalculator.CurrentPlanValue(item)
                        })
                        .ToList()
                })
                .ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await db.HouseholdItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .Include(item => item.SelectedProductChoice)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (item is null)
            return NotFound();

        var optionTotals = item.ProductChoices
            .Select(choice => choice.Price * choice.Quantity)
            .ToList();

        return View(new ItemDetailsViewModel
        {
            Item = item,
            LoggedOptionsValue = HouseholdItemValueCalculator.LoggedOptionsValue(item),
            CheapestOptionValue = HouseholdItemValueCalculator.CheapestOptionValue(item),
            HighestOptionValue = optionTotals.Count == 0 ? null : optionTotals.Max(),
            PreferredOptionValue = HouseholdItemValueCalculator.PreferredPlanValue(item),
            PurchasedChoicesValue = HouseholdItemValueCalculator.PurchasedChoicesValue(item),
            PurchasedValue = HouseholdItemValueCalculator.PurchasedValue(item),
            PreferredOptionCount = item.ProductChoices.Count(choice => choice.IsPreferred)
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!CanEdit()) return LoginRedirect();

        var model = new ItemFormViewModel();
        await PopulateCategories(model);

        return View(model);
    }

    public async Task<IActionResult> BulkCreate()
    {
        if (!CanEdit()) return LoginRedirect();

        var model = new BulkCreateItemsViewModel
        {
            MarkAsPurchased = true,
            Rows = Enumerable.Range(0, 6)
                .Select(_ => new BulkCreateItemRowViewModel())
                .ToList()
        };

        await PopulateBulkCategories(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreate(
        BulkCreateItemsViewModel model,
        CancellationToken cancellationToken)
    {
        if (!CanEdit()) return LoginRedirect();

        if (model.Rows.Count > 200)
            ModelState.AddModelError(nameof(model.Rows), "Add no more than 200 items in one batch.");

        var submittedRows = model.Rows
            .Select((row, index) => new { Row = row, Index = index })
            .Where(entry => !entry.Row.IsBlank)
            .ToList();

        if (submittedRows.Count == 0)
            ModelState.AddModelError(nameof(model.Rows), "Enter at least one item to add.");

        var validCategoryIds = (await db.Categories
            .AsNoTracking()
            .Select(category => category.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var entry in submittedRows)
        {
            var row = entry.Row;
            var keyPrefix = $"Rows[{entry.Index}]";
            var categoryId = row.CategoryId ?? model.DefaultCategoryId;

            if (string.IsNullOrWhiteSpace(row.Name))
                ModelState.AddModelError($"{keyPrefix}.Name", "Enter an item name.");

            if (!categoryId.HasValue || !validCategoryIds.Contains(categoryId.Value))
                ModelState.AddModelError($"{keyPrefix}.CategoryId", "Select a valid category.");

            if (!row.Price.HasValue)
                ModelState.AddModelError($"{keyPrefix}.Price", "Enter the unit price.");

            if (row.Price is > 0
                && row.Quantity > 0
                && row.Price.Value > 99_999_999.99m / row.Quantity)
                ModelState.AddModelError($"{keyPrefix}.Price", "The price multiplied by quantity is too large.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateBulkCategories(model, cancellationToken);
            return View(model);
        }

        var now = DateTime.UtcNow;
        DateTime? purchasedOn = model.MarkAsPurchased ? DateTime.Today : null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var entry in submittedRows)
        {
            var row = entry.Row;
            var name = row.Name!.Trim();
            var categoryId = row.CategoryId ?? model.DefaultCategoryId!.Value;
            var price = row.Price!.Value;
            var totalPrice = price * row.Quantity;

            var item = new HouseholdItem
            {
                Name = name,
                CategoryId = categoryId,
                ChoiceType = row.Quantity > 1
                    ? ItemChoiceType.MultipleRequired
                    : ItemChoiceType.SinglePick,
                Priority = ItemPriority.Medium,
                Status = model.MarkAsPurchased
                    ? PurchaseStatus.Purchased
                    : PurchaseStatus.Comparing,
                QuantityRequired = row.Quantity,
                TargetBudget = totalPrice,
                IsEssentialForMoveIn = false,
                PurchasedOn = purchasedOn,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            item.ProductChoices.Add(new ProductChoice
            {
                Name = name,
                Tier = ProductTier.Standard,
                Price = price,
                Quantity = row.Quantity,
                Retailer = "Temu",
                IsPreferred = false,
                IsPurchased = model.MarkAsPurchased,
                PriceCheckedOn = DateTime.Today,
                CreatedAtUtc = now
            });

            db.HouseholdItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var purchasedSuffix = model.MarkAsPurchased ? " as purchased" : string.Empty;
        TempData["Success"] = $"{submittedRows.Count} Temu item{(submittedRows.Count == 1 ? "" : "s")} added{purchasedSuffix}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemFormViewModel model)
    {
        if (!CanEdit()) return LoginRedirect();

        if (!ModelState.IsValid)
        {
            await PopulateCategories(model);
            return View(model);
        }

        var item = new HouseholdItem();
        ApplyForm(item, model);

        db.Add(item);
        await db.SaveChangesAsync();

        if (item.Status == PurchaseStatus.Purchased && item.ActualPurchasePrice.HasValue)
        {
            TempData["Success"] = $"{item.Name} was added with its purchase total.";
            return RedirectToAction(nameof(Details), new { id = item.Id });
        }

        TempData["Success"] = $"{item.Name} was added. Add its first product choice.";

        return RedirectToAction("Create", "ProductChoices", new { itemId = item.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await db.HouseholdItems
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id);

        if (item is null)
            return NotFound();

        var model = new ItemFormViewModel
        {
            Id = item.Id,
            Name = item.Name,
            CategoryId = item.CategoryId,
            ChoiceType = item.ChoiceType,
            Priority = item.Priority,
            Status = item.Status,
            QuantityRequired = item.QuantityRequired,
            TargetBudget = item.TargetBudget,
            PurchasedPrice = item.ActualPurchasePrice,
            IsEssentialForMoveIn = item.IsEssentialForMoveIn,
            NeededBy = item.NeededBy,
            GeneralNotes = item.GeneralNotes,
            DecisionReason = item.DecisionReason,
            FullComparisonResponse = item.FullComparisonResponse
        };

        await PopulateCategories(model);

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemFormViewModel model)
    {
        if (!CanEdit()) return LoginRedirect();

        if (model.Id != id)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateCategories(model);
            return View(model);
        }

        var item = await db.HouseholdItems
            .SingleOrDefaultAsync(item => item.Id == id);

        if (item is null)
            return NotFound();

        ApplyForm(item, model);
        item.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{item.Name} was updated.";

        return RedirectToAction(nameof(Details), new { id = item.Id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await db.HouseholdItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .SingleOrDefaultAsync(item => item.Id == id);

        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await db.HouseholdItems
            .SingleOrDefaultAsync(item => item.Id == id);

        if (item is null)
            return NotFound();

        var name = item.Name;

        if (item.SelectedProductChoiceId.HasValue)
        {
            item.SelectedProductChoiceId = null;
            await db.SaveChangesAsync();
        }

        db.Remove(item);
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{name} and its purchase options were deleted.";

        return RedirectToAction(nameof(Index));
    }

    private static void ApplyForm(HouseholdItem item, ItemFormViewModel model)
    {
        item.Name = model.Name.Trim();
        item.CategoryId = model.CategoryId;
        item.ChoiceType = model.ChoiceType;
        item.Priority = model.Priority;
        item.QuantityRequired = model.QuantityRequired;
        item.TargetBudget = model.TargetBudget;

        // Entering a manual purchase total is itself enough to record the item as
        // purchased. This prevents a valid total being silently discarded when
        // the user forgets to change the status dropdown first.
        var isPurchased = model.Status == PurchaseStatus.Purchased
            || model.PurchasedPrice.HasValue;

        item.Status = isPurchased
            ? PurchaseStatus.Purchased
            : model.Status;

        if (isPurchased)
        {
            item.ActualPurchasePrice = model.PurchasedPrice;
            item.PurchasedOn ??= DateTime.Today;
        }
        else
        {
            item.ActualPurchasePrice = null;
            item.PurchasedOn = null;
        }

        item.IsEssentialForMoveIn = model.IsEssentialForMoveIn;
        item.NeededBy = model.NeededBy;
        item.GeneralNotes = string.IsNullOrWhiteSpace(model.GeneralNotes) ? null : model.GeneralNotes.Trim();
        item.DecisionReason = string.IsNullOrWhiteSpace(model.DecisionReason) ? null : model.DecisionReason.Trim();
        item.FullComparisonResponse = string.IsNullOrWhiteSpace(model.FullComparisonResponse) ? null : model.FullComparisonResponse.Trim();
    }

    private async Task PopulateCategories(ItemFormViewModel model)
    {
        model.Categories = await db.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new SelectListItem(
                category.Name,
                category.Id.ToString()))
            .ToListAsync();
    }

    private async Task PopulateBulkCategories(
        BulkCreateItemsViewModel model,
        CancellationToken cancellationToken = default)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        model.DefaultCategoryId ??= categories
            .FirstOrDefault(category => category.Name == "Kitchen")?.Id
            ?? categories.FirstOrDefault()?.Id;

        model.Categories = categories.Select(category => new SelectListItem(
            category.Name,
            category.Id.ToString()));
    }
}
