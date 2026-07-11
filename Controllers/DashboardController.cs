using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class DashboardController(FinanceRepository repo, IConfiguration config) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var now = DateTime.Today;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        await repo.EnsureModernTablesAsync();

        var fallbackIncome = decimal.TryParse(config["FinanceSettings:DefaultMonthlyIncome"], out var defaultIncome)
            ? defaultIncome
            : 3600m;
        var income = await repo.GetIncomeAsync(y, m);
        var monthlyIncome = income?.Amount ?? await repo.GetMonthlyAllowanceAsync(m, fallbackIncome);

        var bills = await repo.GetRowsAsync("bills", m, y);
        var expenses = await repo.GetRowsAsync("extra_expenses", m, y);
        var investments = await repo.GetRowsAsync("investments", m, y);
        var reserveAllocations = await repo.GetRowsAsync("savings", m, y);
        var reserve = await repo.GetHouseholdReserveAsync();
        var reservePots = await repo.GetReservePotsAsync();

        var vm = new DashboardViewModel
        {
            Year = y,
            Month = m,
            MonthlyIncome = monthlyIncome,
            SickDays = income?.SickDays ?? 0,
            Bills = bills,
            Expenses = expenses,
            Investments = investments,
            Savings = reserveAllocations,
            BillsTotal = bills.Sum(x => x.Amount),
            ExpensesTotal = expenses.Sum(x => x.Amount),
            InvestmentsTotal = investments.Sum(x => x.Amount),
            SavingsTotal = reserveAllocations.Sum(x => x.Amount),
            TotalGoalBalance = reserve.Balance,
            Accounts = [],
            LastModified = [new LastModifiedInfo("Household reserve", reserve.UpdatedAt)]
        };

        ViewBag.ReserveProvider = reserve.Provider;
        ViewBag.ReserveInterestRate = reserve.InterestRate;
        var reserveAllocated = reservePots.Where(p => p.IsActive).Sum(p => p.AllocatedAmount);
        ViewBag.ReserveAllocated = reserveAllocated;
        ViewBag.ReserveUnallocated = reserve.Balance - reserveAllocated;
        ViewBag.DefaultReserveContribution = reservePots.Where(p => p.IsActive).Sum(p => p.DefaultMonthlyContribution);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIncome(int year, int month, decimal amount, int sickDays)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveIncomeAsync(year, month, Math.Max(0, amount), Math.Max(0, sickDays));
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(int year, int month, string source, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.AddPaymentAsync(source, name, Math.Max(0, amount), date == default ? new DateTime(year, month, 1) : date, category, type, length, notes);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePayment(int year, int month, string source, int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.DeletePaymentAsync(source, id);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpGet]
    public async Task<IActionResult> EditPayment(string source, int id, int year, int month)
    {
        if (!CanEdit()) return LoginRedirect();
        var item = await repo.GetPaymentAsync(source, id);
        if (item is null) return NotFound();
        ViewBag.Year = year;
        ViewBag.Month = month;
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPayment(int year, int month, string source, int id, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdatePaymentAsync(source, id, name, Math.Max(0, amount), date, category, type, length, notes);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarryOver(int year, int month, string[] sections)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.CarryOverAsync(year, month, sections);
        var next = new DateTime(year, month, 1).AddMonths(1);
        TempData["CarryMessage"] = $"Selected monthly items copied to {next:MMMM yyyy}.";
        return RedirectToAction(nameof(Index), new { year = next.Year, month = next.Month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AllocateRemainingToReserve(int year, int month, decimal amount)
    {
        if (!CanEdit()) return LoginRedirect();
        if (amount <= 0) return RedirectToAction(nameof(Index), new { year, month });
        await repo.AddPaymentAsync("savings", "Remaining monthly surplus", amount, new DateTime(year, month, 1), null, null, null, "Allocated to the household reserve");
        TempData["Success"] = $"£{amount:N2} allocated to the household reserve for this month.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
