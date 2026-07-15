using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class RemindersController(FinanceRepository repo) : Controller
{
    public async Task<IActionResult> Index(string status = "Open")
    {
        var pots = await repo.GetReservePotsAsync();
        var summaries = await repo.GetReservePotFundingSummariesAsync(pots);
        await repo.SyncFundingRemindersAsync(pots, summaries);

        return View(new FinanceRemindersViewModel
        {
            Status = status,
            Pots = pots,
            Reminders = await repo.GetFinanceRemindersAsync(status)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int? reservePotId, string title, string? description, DateTime dueDate)
    {
        if (!CanEdit()) return LoginRedirect();
        try
        {
            await repo.AddReminderAsync(reservePotId, title, description, dueDate);
            TempData["Success"] = "Reminder added.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdateReminderStatusAsync(id, "Completed");
        TempData["Success"] = "Reminder completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdateReminderStatusAsync(id, "Dismissed");
        TempData["Success"] = "Reminder dismissed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.UpdateReminderStatusAsync(id, "Open");
        TempData["Success"] = "Reminder reopened.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Snooze(int id, DateTime snoozedUntil)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SnoozeReminderAsync(id, snoozedUntil);
        TempData["Success"] = $"Reminder snoozed until {snoozedUntil:dd MMM yyyy}.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
