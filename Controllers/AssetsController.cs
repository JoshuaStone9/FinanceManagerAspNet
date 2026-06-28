using FinanceManagerAspNet.Data;
using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManagerAspNet.Controllers;

public sealed class AssetsController(FinanceRepository repo, MarketPriceService prices, AppDbContext vaultDb) : Controller
{
    public async Task<IActionResult> Index()
    {
        await repo.EnsureModernTablesAsync();
        var emergency = await repo.GetEmergencyFundAsync();
        var accounts = await repo.GetAccountsAsync(emergency);
        var vaultInvestedCategoryNames = new[] { "Coins & Bullion", "Coins", "Bullion", "Gold", "Silver" };
        var vaultTotalValue = await vaultDb.Items
            .Where(i => i.Category == null || !vaultInvestedCategoryNames.Contains(i.Category.Name))
            .SumAsync(i => i.CurrentValue ?? 0);

        var vm = new AssetsViewModel
        {
            Assets = await repo.GetAssetHoldingsAsync(),
            Summary = await repo.GetAssetSummaryAsync(),
            GoalAccountsTotal = accounts.Where(a => a.IncludeInGlobalGoal).Sum(a => a.Amount),
            VaultTotalValue = vaultTotalValue
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Save(AssetHolding asset)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveAssetHoldingAsync(asset);
        TempData["Success"] = asset.Id == 0 ? "Asset added." : "Asset updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeleteAssetHoldingAsync(id);
        TempData["Success"] = "Asset deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateLivePrices(CancellationToken cancellationToken)
    {
        if (!CanEdit()) return LoginRedirect();
        var assets = await repo.GetAssetHoldingsAsync();
        var updated = 0;
        var failed = new List<string>();

        foreach (var asset in assets.Where(a => a.UseLivePrice))
        {
            if (asset.IsBullion && !asset.UsesSpotValuation)
            {
                // Proof/collectible coins use manual/market valuation, not melt value.
                continue;
            }

            var price = await prices.GetLivePriceGbpAsync(asset, cancellationToken);
            if (price.HasValue && price.Value > 0)
            {
                await repo.UpdateAssetLivePriceAsync(asset.Id, price.Value, DateTime.UtcNow);
                updated++;
            }
            else
            {
                failed.Add(string.IsNullOrWhiteSpace(asset.Symbol) ? asset.Name : $"{asset.Name} ({asset.Symbol})");
            }
        }

        TempData["Success"] = failed.Count == 0
            ? $"Portfolio synced. Updated {updated} live price{(updated == 1 ? "" : "s")}."
            : $"Portfolio synced with warnings. Updated {updated}. Could not update: {string.Join(", ", failed.Take(6))}{(failed.Count > 6 ? "..." : "")}.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
