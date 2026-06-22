using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class DashboardController(FinanceRepository repo, FinanceCalculator calc, IConfiguration config) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var now = DateTime.Today;
        var y = year ?? now.Year; var m = month ?? now.Month;
        await repo.EnsureModernTablesAsync();
        var fallbackIncome = decimal.TryParse(config["FinanceSettings:DefaultMonthlyIncome"], out var di) ? di : 3500m;
        var monthlyTarget = decimal.TryParse(config["FinanceSettings:MonthlySavingTarget"], out var mt) ? mt : 1200m;
        var globalGoal = decimal.TryParse(config["FinanceSettings:GlobalGoal"], out var gg) ? gg : 20000m;
        var income = await repo.GetIncomeAsync(y, m);
        var allowance = income?.Amount ?? await repo.GetMonthlyAllowanceAsync(m, fallbackIncome);
        var emergency = await repo.GetEmergencyFundAsync();
        var accounts = await repo.GetAccountsAsync(emergency);
        var targetDate = new DateTime(now.Year + (now.Month > 4 ? 1 : 0), 4, 30);
        var monthsToApril = Math.Max(0, ((targetDate.Year - now.Year) * 12) + targetDate.Month - now.Month);
        var bills = await repo.GetRowsAsync("bills", m, y);
        var expenses = await repo.GetRowsAsync("extra_expenses", m, y);
        var investments = await repo.GetRowsAsync("investments", m, y);
        var savings = await repo.GetRowsAsync("savings", m, y);
        var totalGoalBalance = accounts.Where(a => a.IncludeInGlobalGoal).Sum(a => a.Amount);
        var vm = new DashboardViewModel
        {
            Year = y, Month = m, MonthlyIncome = allowance, SickDays = income?.SickDays ?? 0,
            MonthlySavingTarget = monthlyTarget, GlobalGoal = globalGoal,
            Bills = bills, Expenses = expenses, Investments = investments, Savings = savings,
            BillsTotal = bills.Sum(x=>x.Amount), ExpensesTotal = expenses.Sum(x=>x.Amount), InvestmentsTotal = investments.Sum(x=>x.Amount), SavingsTotal = savings.Sum(x=>x.Amount),
            Accounts = accounts, TotalGoalBalance = totalGoalBalance, TargetDate = targetDate,
            ProjectedWithoutInterestByGoalDate = calc.ProjectAccountsWithoutInterest(accounts, monthsToApril, monthlyTarget),
            ProjectedWithInterestByGoalDate = calc.ProjectAccounts(accounts, monthsToApril, monthlyTarget),
            ProjectedInterestByGoalDate = calc.ProjectAccounts(accounts, monthsToApril, monthlyTarget) - calc.ProjectAccountsWithoutInterest(accounts, monthsToApril, monthlyTarget),
            ProjectedSalarySavingsByGoalDate = calc.ProjectSalarySavings(monthlyTarget, monthsToApril),
            LastModified = accounts.Select(a => new LastModifiedInfo(a.Name, a.UpdatedAt == DateTime.MinValue ? null : a.UpdatedAt)).ToList()
        };
        var pace = Math.Max(0, vm.RemainingFund);
        vm.MonthsToGoalAtCurrentPace = calc.MonthsToGoal(totalGoalBalance, globalGoal, pace);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> SaveIncome(int year, int month, decimal amount, int sickDays)
    { await repo.SaveIncomeAsync(year, month, amount, sickDays); return RedirectToAction(nameof(Index), new { year, month }); }

    [HttpPost]
    public async Task<IActionResult> SaveAccount(int year, int month, int id, string name, decimal amount, decimal interestRate, decimal monthlyContribution, bool includeInGlobalGoal = true)
    { await repo.SaveAccountAsync(id, name, amount, interestRate, monthlyContribution, includeInGlobalGoal); return RedirectToAction(nameof(Index), new { year, month }); }



    [HttpPost]
    public async Task<IActionResult> AddPayment(int year, int month, string source, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        await repo.AddPaymentAsync(source, name, amount, date, category, type, length, notes);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpGet]
    public async Task<IActionResult> EditPayment(string source, int id, int year, int month)
    {
        var item = await repo.GetPaymentAsync(source, id);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpPost]
    public async Task<IActionResult> EditPayment(int year, int month, string source, int id, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        await repo.UpdatePaymentAsync(source, id, name, amount, date, category, type, length, notes);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    public async Task<IActionResult> CarryOver(int year, int month, string[] sections)
    {
        var carryAmount = await repo.CarryOverAsync(year, month, sections);
        var next = new DateTime(year, month, 1).AddMonths(1);
        TempData["CarryMessage"] = carryAmount == 0 ? "No carry amount generated." : (carryAmount < 0 ? $"Shortfall carried: {Math.Abs(carryAmount):C}" : $"Surplus carried: {carryAmount:C}");
        return RedirectToAction(nameof(Index), new { year = next.Year, month = next.Month });
    }
}
