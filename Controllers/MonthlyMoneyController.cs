using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class MonthlyMoneyController(FinanceRepository repo) : Controller
{
    public async Task<IActionResult> Income(int? year, int? month)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;
        if (selectedMonth is < 1 or > 12) return BadRequest("Month must be between 1 and 12.");

        var model = new MonthlyIncomeWorkspaceViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            Entries = await repo.GetMonthlyIncomeEntriesAsync(selectedYear, selectedMonth),
            MissingRecurringEntries = await repo.GetMissingRecurringIncomeEntriesAsync(selectedYear, selectedMonth)
        };
        return View("Income", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddIncome(int year, int month, string name, decimal amount, DateTime date, string? category, string? notes, bool isRecurring = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (month is < 1 or > 12 || string.IsNullOrWhiteSpace(name) || amount < 0) return BadRequest("Enter a valid income source and amount.");
        var selectedDate = date == default ? new DateTime(year, month, Math.Min(DateTime.Today.Day, DateTime.DaysInMonth(year, month))) : date;
        await repo.AddMonthlyIncomeEntryAsync(name, amount, selectedDate, category, notes, isRecurring);
        TempData["Success"] = $"{name.Trim()} income added.";
        return RedirectToAction(nameof(Income), new { year, month, focus = "quick-entry" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateIncome(int id, int year, int month, string name, decimal amount, DateTime date, string? category, string? notes, bool isRecurring = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(name) || amount < 0) return BadRequest("Enter a valid income source and amount.");
        await repo.UpdateMonthlyIncomeEntryAsync(id, name, amount, date, category, notes, isRecurring);
        TempData["Success"] = $"{name.Trim()} income updated.";
        var url = Url.Action(nameof(Income), new { year, month });

        return Redirect($"{url}#entry-{id}");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIncome(int id, int year, int month)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.DeleteMonthlyIncomeEntryAsync(id);
        TempData["Success"] = "Income entry deleted.";
        return RedirectToAction(nameof(Income), new { year, month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetIncomeRecurring(int id, int year, int month, bool isRecurring)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.SetMonthlyIncomeRecurringAsync(id, isRecurring);
        return Json(new { success = true, isRecurring, message = isRecurring ? "Income now repeats monthly." : "Income is now one-off." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupIncomeMonth(int year, int month, List<int> entryIds)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var added = await repo.SetupRecurringIncomeAsync(year, month, entryIds ?? []);
        TempData["Success"] = added == 0 ? "No new recurring income was added." : $"{added} recurring income entr{(added == 1 ? "y" : "ies")} added.";
        return RedirectToAction(nameof(Income), new { year, month });
    }
    public Task<IActionResult> EssentialBills(int? year, int? month)
        => BuildWorkspaceAsync(
            "bills",
            "EssentialBills",
            "Essential bills",
            "Fixed commitments",
            "Manage mortgage, council tax, utilities, insurance and other recurring commitments.",
            "receipt",
            "No essential bills recorded",
            "Add the first fixed commitment for this month.",
            "Add essential bill",
            "Choose a previous bill",
            "Bill name",
            "Description",
            supportsType: true,
            supportsLength: true,
            isCarryOverEligible: true,
            year: year,
            month: month);

    public Task<IActionResult> EverydaySpending(int? year, int? month)
        => BuildWorkspaceAsync(
            "everyday_spending",
            "EverydaySpending",
            "Everyday spending",
            "Normal monthly allowances",
            "Manage food, fuel and the flexible spending amounts used during the selected month.",
            "shopping-basket",
            "No everyday spending recorded",
            "Add the first normal monthly allowance or spending entry.",
            "Add everyday spending",
            "Choose a previous entry",
            "Entry name",
            "Notes",
            supportsCategory: true,
            supportsType: true,
            supportsLength: true,
            isCarryOverEligible: true,
            year: year,
            month: month);

    public Task<IActionResult> ExtraExpenses(int? year, int? month)
        => BuildWorkspaceAsync(
            "extra_expenses",
            "ExtraExpenses",
            "Extra expenses",
            "One-off monthly costs",
            "Keep unexpected and non-recurring costs separate from normal monthly spending.",
            "circle-plus",
            "No extra expenses this month",
            "One-off expenses are excluded from Carry over all.",
            "Add extra expense",
            "Copy a previous expense",
            "Expense name",
            "Reason or notes",
            supportsCategory: true,
            supportsType: true,
            isCarryOverEligible: false,
            year: year,
            month: month);

    public Task<IActionResult> Investments(int? year, int? month)
        => BuildWorkspaceAsync(
            "investments",
            "Investments",
            "Investments",
            "Monthly contributions",
            "Record contributions made to investments without mixing them with current asset valuations.",
            "chart-pie",
            "No investment contributions recorded",
            "Add the first contribution for the selected month.",
            "Add investment contribution",
            "Choose a previous investment",
            "Investment name",
            "Provider or notes",
            supportsCategory: true,
            supportsLength: true,
            isCarryOverEligible: true,
            year: year,
            month: month);

    public Task<IActionResult> MoneyPots(int? year, int? month)
        => BuildWorkspaceAsync(
            "savings",
            "MoneyPots",
            "Money pots",
            "Monthly pot funding",
            "Record contributions to your existing money pots while keeping pot goals and settings on the main Money Pots page.",
            "piggy-bank",
            "No money pot contributions recorded",
            "Choose an active money pot and record the first contribution for this month.",
            "Add money pot contribution",
            "Choose a money pot",
            "Money pot",
            "Contribution notes",
            isCarryOverEligible: true,
            isMoneyPots: true,
            year: year,
            month: month);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPermanent(
        string source,
        int id,
        int year,
        int month,
        bool isPermanent)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        if (source == "extra_expenses")
            return BadRequest("Extra expenses are always one-off entries.");

        var entry = await repo.GetPaymentAsync(source, id);
        if (entry is null)
            return NotFound();

        string message;

        if (isPermanent)
        {
            await repo.UpsertMonthlyEntryTemplateAsync(
                source,
                entry.Name,
                entry.Amount,
                entry.Category,
                entry.Type,
                entry.Length,
                entry.Notes);
            message = $"{entry.DisplayName} now repeats monthly.";
        }
        else
        {
            await repo.SetMonthlyEntryTemplateActiveAsync(source, entry.Name, false);
            message = $"{entry.DisplayName} is now a one-off entry.";
        }

        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new
            {
                success = true,
                isPermanent,
                message
            });
        }

        TempData["Success"] = message;
        var destination = Url.Action(GetWorkspaceAction(source), new { year, month });
        return Redirect($"{destination}#entry-{id}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupMonth(MonthSetupInput input)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        if (input.Month is < 1 or > 12)
            return BadRequest("Month must be between 1 and 12.");

        var added = await repo.SetupMonthFromTemplatesAsync(
            input.Year,
            input.Month,
            input.Source,
            input.Items);

        TempData["Success"] = added == 0
            ? "No new recurring entries were added. Existing entries were left unchanged."
            : $"{added} recurring entr{(added == 1 ? "y" : "ies")} added to {new DateTime(input.Year, input.Month, 1):MMMM yyyy}.";

        return RedirectToAction(GetWorkspaceAction(input.Source), new
        {
            year = input.Year,
            month = input.Month,
            focus = "monthly-review"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAmount(
        string source,
        int id,
        decimal amount,
        int year,
        int month)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        if (month is < 1 or > 12)
            return BadRequest("Month must be between 1 and 12.");

        if (amount < 0)
            return BadRequest("Amount cannot be negative.");

        var existing = await repo.GetPaymentAsync(source, id);
        if (existing is null)
            return NotFound();

        await repo.UpdatePaymentAsync(
            source,
            id,
            existing.Name,
            amount,
            existing.Date,
            existing.Category,
            existing.Type,
            existing.Length,
            existing.Notes);

        if (source == "savings")
            await repo.ApplyReserveAllocationAsync(existing.Name, amount - existing.Amount);

        TempData["Success"] = $"{existing.DisplayName} amount updated.";

        var actionName = GetWorkspaceAction(source);
        var destination = Url.Action(actionName, new { year, month });
        return Redirect($"{destination}#entry-{id}");
    }

    private static string GetWorkspaceAction(string source)
        => source switch
        {
            "income" => nameof(Income),
            "bills" => nameof(EssentialBills),
            "everyday_spending" => nameof(EverydaySpending),
            "extra_expenses" => nameof(ExtraExpenses),
            "investments" => nameof(Investments),
            "savings" => nameof(MoneyPots),
            _ => throw new ArgumentOutOfRangeException(nameof(source), "Unknown monthly money workspace.")
        };

    private async Task<IActionResult> BuildWorkspaceAsync(
        string source,
        string actionName,
        string title,
        string eyebrow,
        string description,
        string icon,
        string emptyTitle,
        string emptyDescription,
        string addTitle,
        string existingLabel,
        string nameLabel,
        string notesLabel,
        bool supportsCategory = false,
        bool supportsType = false,
        bool supportsLength = false,
        bool isCarryOverEligible = false,
        bool isMoneyPots = false,
        int? year = null,
        int? month = null)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        if (selectedMonth is < 1 or > 12)
            return BadRequest("Month must be between 1 and 12.");

        await repo.EnsureModernTablesAsync();

        List<MonthlyEntryTemplate> permanentTemplates = source == "extra_expenses"
            ? []
            : await repo.GetMonthlyEntryTemplatesAsync(source);
        List<MonthlyEntryTemplate> missingPermanentTemplates = source == "extra_expenses"
            ? []
            : await repo.GetMissingMonthlyEntryTemplatesAsync(source, selectedYear, selectedMonth);

        var model = new MonthlyMoneyWorkspaceViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            Source = source,
            ActionName = actionName,
            Title = title,
            Eyebrow = eyebrow,
            Description = description,
            Icon = icon,
            EmptyTitle = emptyTitle,
            EmptyDescription = emptyDescription,
            AddTitle = addTitle,
            ExistingLabel = existingLabel,
            NameLabel = nameLabel,
            NotesLabel = notesLabel,
            SupportsCategory = supportsCategory,
            SupportsType = supportsType,
            SupportsLength = supportsLength,
            IsCarryOverEligible = isCarryOverEligible,
            IsMoneyPots = isMoneyPots,
            Rows = await repo.GetRowsAsync(source, selectedMonth, selectedYear),
            ExistingOptions = await repo.GetExistingPaymentOptionsAsync(source),
            PermanentTemplates = permanentTemplates,
            MissingPermanentTemplates = missingPermanentTemplates,
            PotOptions = isMoneyPots
                ? (await repo.GetReservePotsAsync()).Where(x => x.IsActive).OrderBy(x => x.Priority).ThenBy(x => x.Name).ToList()
                : []
        };

        return View("Workspace", model);
    }
}
