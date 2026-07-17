using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class MonthlyMoneyController(FinanceRepository repo) : Controller
{
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
        int? year = null,
        int? month = null)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        if (selectedMonth is < 1 or > 12)
            return BadRequest("Month must be between 1 and 12.");

        await repo.EnsureModernTablesAsync();

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
            Rows = await repo.GetRowsAsync(source, selectedMonth, selectedYear),
            ExistingOptions = await repo.GetExistingPaymentOptionsAsync(source)
        };

        return View("Workspace", model);
    }
}
