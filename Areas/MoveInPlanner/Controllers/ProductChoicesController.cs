using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Models.Entities;
using MoveInPlanner.Models.Enums;
using MoveInPlanner.Models.ViewModels;
using MoveInPlanner.Models.Requests;
using MoveInPlanner.Services.ProductMetadata;

namespace MoveInPlanner.Controllers;

[Area("MoveInPlanner")]
public class ProductChoicesController(
    ApplicationDbContext db,
    IProductMetadataService productMetadataService,
    ProductImageRequestPolicy imageRequestPolicy,
    IHttpClientFactory httpClientFactory) : MoveInPlannerController
{

    /// <summary>
    /// Proxies approved retailer product images through the application. Some retailer
    /// image hosts reject browser hotlinks even when the URL was imported correctly.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> ImagePreview(
        string url,
        CancellationToken cancellationToken)
    {
        if (!imageRequestPolicy.TryCreate(url, out var imageUri,out var referer))
            return BadRequest();

        try
        {
            var client = httpClientFactory.CreateClient("ProductImageProxy");
            using var request = new HttpRequestMessage(HttpMethod.Get, imageUri);
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Referer", referer.ToString());

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return NotFound();

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0 || bytes.Length > 10_000_000)
                return NotFound();

            return File(bytes, mediaType);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return NotFound();
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchProductMetadata(
        ProductMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanEdit()) return LoginRedirect();

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Enter a valid Amazon or TikTok Shop product URL."
            });
        }

        var result = await productMetadataService.GetFromUrlAsync(
            request.ProductUrl,
            cancellationToken);

        return Json(result);
    }

    public async Task<IActionResult> Create(int itemId)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await db.HouseholdItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == itemId);

        if (item is null) return NotFound();

        return View(new ProductChoiceFormViewModel
        {
            HouseholdItemId = item.Id,
            HouseholdItemName = item.Name,
            Quantity = item.QuantityRequired,
            PriceCheckedOn = DateTime.Today
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductChoiceFormViewModel model)
    {
        if (!CanEdit()) return LoginRedirect();

        var item = await db.HouseholdItems
            .SingleOrDefaultAsync(x => x.Id == model.HouseholdItemId);

        if (item is null) return NotFound();

        model.HouseholdItemName = item.Name;

        if (!ModelState.IsValid)
            return View(model);

        var choice = new ProductChoice
        {
            HouseholdItemId = item.Id,
            Name = model.Name.Trim(),
            Tier = model.Tier,
            Price = model.Price,
            Quantity = model.Quantity,
            ProductUrl = NormaliseUrl(model.ProductUrl),
            ImageUrl = NormaliseUrl(model.ImageUrl),
            Retailer = string.IsNullOrWhiteSpace(model.Retailer) ? null : model.Retailer.Trim(),
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            IsPreferred = model.IsPreferred,
            IsPurchased = model.IsPurchased,
            PriceCheckedOn = model.PriceCheckedOn
        };

        db.ProductChoices.Add(choice);
        await db.SaveChangesAsync();

        await SynchroniseItemState(item.Id);

        TempData["Success"] = $"{choice.Name} was added.";
        return RedirectToAction("Details", "Items", new { id = item.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var choice = await db.ProductChoices.AsNoTracking()
            .Include(x => x.HouseholdItem)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (choice is null) return NotFound();

        return View(new ProductChoiceFormViewModel
        {
            Id = choice.Id,
            HouseholdItemId = choice.HouseholdItemId,
            HouseholdItemName = choice.HouseholdItem.Name,
            Name = choice.Name,
            Tier = choice.Tier,
            Price = choice.Price,
            Quantity = choice.Quantity,
            ProductUrl = choice.ProductUrl,
            ImageUrl = choice.ImageUrl,
            Retailer = choice.Retailer,
            Notes = choice.Notes,
            IsPreferred = choice.IsPreferred,
            IsPurchased = choice.IsPurchased,
            PriceCheckedOn = choice.PriceCheckedOn
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductChoiceFormViewModel model)
    {
        if (!CanEdit()) return LoginRedirect();

        if (!model.Id.HasValue) return BadRequest();

        var choice = await db.ProductChoices
            .Include(x => x.HouseholdItem)
            .SingleOrDefaultAsync(x => x.Id == model.Id && x.HouseholdItemId == model.HouseholdItemId);

        if (choice is null) return NotFound();

        model.HouseholdItemName = choice.HouseholdItem.Name;

        if (!ModelState.IsValid)
            return View(model);

        choice.Name = model.Name.Trim();
        choice.Tier = model.Tier;
        choice.Price = model.Price;
        choice.Quantity = model.Quantity;
        choice.ProductUrl = NormaliseUrl(model.ProductUrl);
        choice.ImageUrl = NormaliseUrl(model.ImageUrl);
        choice.Retailer = string.IsNullOrWhiteSpace(model.Retailer) ? null : model.Retailer.Trim();
        choice.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        choice.IsPreferred = model.IsPreferred;
        choice.IsPurchased = model.IsPurchased;
        choice.PriceCheckedOn = model.PriceCheckedOn;

        await db.SaveChangesAsync();
        await SynchroniseItemState(choice.HouseholdItemId);

        TempData["Success"] = $"{choice.Name} was updated.";
        return RedirectToAction("Details", "Items", new { id = choice.HouseholdItemId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MakePreferred(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var choice = await db.ProductChoices
            .SingleOrDefaultAsync(x => x.Id == id);

        if (choice is null) return NotFound();

        // Preferred choices are additive. This allows an overall household item to
        // contain two or more products that are all part of the planned purchase.
        choice.IsPreferred = !choice.IsPreferred;
        await db.SaveChangesAsync();
        await SynchroniseItemState(choice.HouseholdItemId);

        TempData["Success"] = choice.IsPreferred
            ? $"{choice.Name} was included in the preferred plan."
            : $"{choice.Name} was removed from the preferred plan.";

        return RedirectToAction("Details", "Items", new { id = choice.HouseholdItemId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanEdit()) return LoginRedirect();

        var choice = await db.ProductChoices
            .Include(x => x.HouseholdItem)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (choice is null) return NotFound();

        var itemId = choice.HouseholdItemId;
        if (choice.HouseholdItem.SelectedProductChoiceId == choice.Id)
            choice.HouseholdItem.SelectedProductChoiceId = null;

        db.ProductChoices.Remove(choice);
        await db.SaveChangesAsync();
        await SynchroniseItemState(itemId);

        TempData["Success"] = "Product choice deleted.";
        return RedirectToAction("Details", "Items", new { id = itemId });
    }

    private async Task SynchroniseItemState(int itemId)
    {
        var item = await db.HouseholdItems
            .Include(x => x.ProductChoices)
            .SingleAsync(x => x.Id == itemId);

        var preferredChoices = item.ProductChoices
            .Where(x => x.IsPreferred)
            .ToList();

        var purchasedChoices = item.ProductChoices
            .Where(x => x.IsPurchased)
            .ToList();

        // The legacy singular relationship is retained for compatibility. It is
        // populated only when exactly one option is preferred; multiple preferred
        // options are represented by ProductChoice.IsPreferred.
        item.SelectedProductChoiceId = preferredChoices.Count == 1
            ? preferredChoices[0].Id
            : null;

        // ActualPurchasePrice is an item-level manual override. Product-choice
        // changes must not replace or clear it; calculated choice totals are read
        // by HouseholdItemValueCalculator when no manual value exists.
        if (item.ActualPurchasePrice.HasValue || purchasedChoices.Count > 0)
        {
            item.Status = PurchaseStatus.Purchased;
            item.PurchasedOn ??= DateTime.Today;
        }
        else
        {
            item.PurchasedOn = null;
            item.Status = preferredChoices.Count > 0
                ? PurchaseStatus.Decided
                : item.ProductChoices.Count > 0
                    ? PurchaseStatus.Comparing
                    : PurchaseStatus.Researching;
        }

        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static string? NormaliseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var value = url.Trim();
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"https://{value}";
    }
}
