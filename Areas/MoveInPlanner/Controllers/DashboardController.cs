using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Models.Enums;
using MoveInPlanner.Models.ViewModels;
using MoveInPlanner.Services;

namespace MoveInPlanner.Controllers;

[Area("MoveInPlanner")]
public class DashboardController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var items = await db.HouseholdItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .Include(item => item.SelectedProductChoice)
            .OrderByDescending(item => item.IsEssentialForMoveIn)
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => item.Name)
            .ToListAsync();

        var model = new DashboardViewModel
        {
            TotalItems = items.Count,

            PurchasedItems = items.Count(item => item.Status == PurchaseStatus.Purchased),

            EssentialOutstanding = items.Count(item => item.IsEssentialForMoveIn && item.Status != PurchaseStatus.Purchased),

            ItemsWithoutChoices = items.Count(item =>
                item.Status != PurchaseStatus.Purchased && item.ProductChoices.Count == 0),

            PlannedBudget = items.Sum(item => item.TargetBudget ?? 0),

            PurchasedSpend = items.Sum(HouseholdItemValueCalculator.PurchasedValue),

            LoggedOptionsValue = items.Sum(HouseholdItemValueCalculator.LoggedOptionsValue),

            CurrentPlanValue = items.Sum(HouseholdItemValueCalculator.CurrentPlanValue),

            Categories = items
                .GroupBy(item => item.Category.Name)
                .OrderBy(group => group.Key)
                .Select(group => new CategoryDashboardGroup
                {
                    Name = group.Key,
                    Total = group.Count(),

                    Purchased = group.Count(item => item.Status == PurchaseStatus.Purchased),

                    Items = group.Take(6).ToList()
                })
                .ToList(),

            NextActions = items
                .Where(item => item.Status != PurchaseStatus.Purchased)
                .OrderByDescending(item => item.IsEssentialForMoveIn)
                .ThenByDescending(item => item.Priority)
                .ThenBy(item => item.NeededBy ?? DateTime.MaxValue)
                .Take(6)
                .ToList()
        };

        return View(model);
    }
}
