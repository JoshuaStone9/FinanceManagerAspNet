using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

[Route("Reminders")]
[Route("FinancialTasks")]
public sealed class RemindersController(FinanceRepository repo) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string status = "Open")
    {
        var pots = await repo.GetReservePotsAsync();
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        await repo.SyncFundingRemindersAsync(pots, summaries);

        var reminders = await repo.GetFinanceRemindersAsync(status);
        var smartTasks = status is "Open" or "All"
            ? await BuildSmartTasksAsync(pots)
            : [];

        return View(new FinanceRemindersViewModel
        {
            Status = status,
            Pots = pots,
            Reminders = reminders,
            SmartTasks = smartTasks
        });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int? reservePotId, string title, string? description, DateTime dueDate)
    {
        if (!CanEdit()) return LoginRedirect();
        try
        {
            await repo.AddReminderAsync(reservePotId, title, description, dueDate);
            TempData["Success"] = "Financial task added.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdateReminderStatusAsync(id, "Completed");
        TempData["Success"] = "Task completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdateReminderStatusAsync(id, "Dismissed");
        TempData["Success"] = "Task dismissed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Reopen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdateReminderStatusAsync(id, "Open");
        TempData["Success"] = "Task reopened.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Snooze")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Snooze(int id, DateTime snoozedUntil)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SnoozeReminderAsync(id, snoozedUntil);
        TempData["Success"] = $"Task snoozed until {snoozedUntil:dd MMM yyyy}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<FinancialTaskItem>> BuildSmartTasksAsync(IReadOnlyList<ReservePot> pots)
    {
        var tasks = new List<FinancialTaskItem>();
        var today = DateTime.Today;
        var emergencyFund = (await repo.GetHouseholdReserveAsync()).Balance;
        var accounts = await repo.GetAccountsAsync(emergencyFund);
        var passiveIncome = await repo.GetPassiveIncomeEstimatesAsync(today.Year, today.Month);

        tasks.AddRange(accounts
            .Where(x => x.Amount > 0m && x.UpdatedAt.Date <= today.AddDays(-90))
            .Select(x => new FinancialTaskItem(
                $"Check {x.Name} balance",
                $"The recorded balance has not been updated since {x.UpdatedAt:dd MMM yyyy}.",
                "Accounts",
                x.UpdatedAt.Date <= today.AddDays(-180) ? "High" : "Medium",
                "~/Reconciliation",
                "scale",
                today)));

        tasks.AddRange(passiveIncome
            .Where(x => !x.IsReceived && x.EstimatedAmount > 0m)
            .Select(x => new FinancialTaskItem(
                $"Confirm {x.SourceName} interest",
                $"Approximately {x.EstimatedAmount:C} is expected this month.",
                "Income",
                "Medium",
                $"~/MonthlyMoney/Income?year={today.Year}&month={today.Month}",
                "badge-pound-sterling",
                new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)))));

        tasks.AddRange(pots
            .Where(x => x.IsActive && x.DueDate.HasValue && x.TargetAmount.HasValue && x.AllocatedAmount < x.TargetAmount.Value)
            .Where(x => x.DueDate!.Value.Date <= today.AddDays(60))
            .Select(x => new FinancialTaskItem(
                $"Review {x.Name}",
                $"{Math.Max(0m, x.TargetAmount!.Value - x.AllocatedAmount):C} remains before {x.DueDate:dd MMM yyyy}.",
                "Money Pot",
                x.DueDate!.Value.Date < today ? "High" : "Medium",
                $"~/SavingPots#pot-{x.Id}",
                "piggy-bank",
                x.DueDate)));

        return tasks
            .OrderBy(x => x.Priority == "High" ? 0 : x.Priority == "Medium" ? 1 : 2)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .Take(12)
            .ToList();
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
