using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class ForecastController(
    FinanceRepository repo,
    IReserveAccountSelectionService reserveAccountSelectionService,
    IFinancialForecastService financialForecastService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int months = 12, DateTime? customEndDate = null, bool includeAccountContributions = true)
    {
        var safeMonths = new[] { 3, 6, 12, 24 }.Contains(months) ? months : 12;
        var endDate = customEndDate?.Date ?? DateTime.Today.AddMonths(safeMonths);
        if (endDate < DateTime.Today) endDate = DateTime.Today;

        var accountSummary = await reserveAccountSelectionService.BuildSummaryAsync();
        var pots = await repo.GetReservePotsAsync();
        var forecast = financialForecastService.Build(new FinancialForecastRequest
        {
            StartDate = DateTime.Today,
            EndDate = endDate,
            ProtectedReserveBaseline = accountSummary.Baseline,
            IncludeAccountContributions = includeAccountContributions,
            Accounts = accountSummary.SelectedAccounts,
            Pots = pots
        });

        return View(new ForecastPageViewModel
        {
            Months = safeMonths,
            CustomEndDate = customEndDate,
            IncludeAccountContributions = includeAccountContributions,
            Forecast = forecast
        });
    }
}
