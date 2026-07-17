using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class ForecastController(
    FinanceRepository repo,
    IReserveAccountSelectionService reserveAccountSelectionService,
    IFinancialForecastService financialForecastService,
    IWhatIfForecastService whatIfForecastService,
    IForecastScenarioService scenarioService) : Controller
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

    [HttpGet]
    public async Task<IActionResult> WhatIf([FromQuery] WhatIfForecastInput input)
    {
        var accountSummary = await reserveAccountSelectionService.BuildSummaryAsync();
        var pots = await repo.GetReservePotsAsync();
        return View(whatIfForecastService.Build(input, accountSummary, pots));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveScenario(string name, WhatIfForecastInput input)
    {
        if (!CanEdit()) return LoginRedirect();
        var id = await scenarioService.SaveAsync(name, input);
        TempData["Success"] = $"Scenario '{(string.IsNullOrWhiteSpace(name) ? "Untitled scenario" : name.Trim())}' saved.";
        return RedirectToAction(nameof(Scenarios), new { selectedId = id });
    }

    [HttpGet]
    public async Task<IActionResult> Scenarios(int? selectedId = null)
    {
        var scenarios = await scenarioService.GetAllAsync();
        var pots = await repo.GetReservePotsAsync();
        ViewBag.SelectedId = selectedId;
        return View(new ForecastScenarioListViewModel
        {
            Scenarios = scenarios,
            PotNames = pots.ToDictionary(x => x.Id, x => x.Name)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameScenario(int id, string name)
    {
        if (!CanEdit()) return LoginRedirect();
        await scenarioService.RenameAsync(id, name);
        TempData["Success"] = "Scenario renamed.";
        return RedirectToAction(nameof(Scenarios), new { selectedId = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateScenario(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        var duplicateId = await scenarioService.DuplicateAsync(id);
        if (!duplicateId.HasValue) return NotFound();
        TempData["Success"] = "Scenario duplicated.";
        return RedirectToAction(nameof(Scenarios), new { selectedId = duplicateId.Value });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteScenario(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await scenarioService.DeleteAsync(id);
        TempData["Success"] = "Scenario deleted.";
        return RedirectToAction(nameof(Scenarios));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPreferredScenario(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await scenarioService.SetPreferredAsync(id);
        TempData["Success"] = "Preferred scenario updated. Live data has not been changed.";
        return RedirectToAction(nameof(Scenarios), new { selectedId = id });
    }

    [HttpGet]
    public async Task<IActionResult> CompareScenarios(int leftId, int rightId)
    {
        var left = await scenarioService.GetAsync(leftId);
        var right = await scenarioService.GetAsync(rightId);
        if (left is null || right is null) return NotFound();

        var accountSummary = await reserveAccountSelectionService.BuildSummaryAsync();
        var pots = await repo.GetReservePotsAsync();
        return View(new ForecastScenarioComparisonViewModel
        {
            LeftScenario = left,
            RightScenario = right,
            Left = whatIfForecastService.Build(left.ToInput(), accountSummary, pots),
            Right = whatIfForecastService.Build(right.ToInput(), accountSummary, pots),
            AvailableScenarios = await scenarioService.GetAllAsync()
        });
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
