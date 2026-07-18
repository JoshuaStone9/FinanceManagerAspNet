using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class MoneyPotActivityController(IMoneyPotActivityService activityService) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var today = DateTime.Today;
        var selectedYear = year is >= 2000 and <= 2100 ? year.Value : today.Year;
        var selectedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;

        return View(await activityService.BuildAsync(selectedYear, selectedMonth));
    }
}
