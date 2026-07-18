using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class FinancialTrendsController(IFinancialTrendsService trendsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int range = 6)
    {
        var model = await trendsService.BuildAsync(range);
        return View(model);
    }
}
