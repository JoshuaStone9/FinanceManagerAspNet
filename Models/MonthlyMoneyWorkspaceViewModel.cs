namespace FinanceManagerAspNet.Models;

public sealed class MonthlyMoneyWorkspaceViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Source { get; init; } = string.Empty;
    public string ActionName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = "Monthly money";
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "circle-dollar-sign";
    public string EmptyTitle { get; init; } = "Nothing entered yet";
    public string EmptyDescription { get; init; } = string.Empty;
    public string AddTitle { get; init; } = "Add entry";
    public string ExistingLabel { get; init; } = "Pick from existing";
    public string NameLabel { get; init; } = "Name";
    public string NotesLabel { get; init; } = "Notes";
    public bool SupportsCategory { get; init; }
    public bool SupportsType { get; init; }
    public bool SupportsLength { get; init; }
    public IReadOnlyList<string> TypeOptions { get; init; } = [];
    public bool IsCarryOverEligible { get; init; }
    public bool IsMoneyPots { get; init; }
    public IReadOnlyList<ReservePot> PotOptions { get; init; } = [];
    public IReadOnlyList<PaymentRow> Rows { get; init; } = [];
    public IReadOnlyList<ExistingPaymentOption> ExistingOptions { get; init; } = [];
    public IReadOnlyList<MonthlyEntryTemplate> PermanentTemplates { get; init; } = [];
    public IReadOnlyList<MonthlyEntryTemplate> MissingPermanentTemplates { get; init; } = [];

    public bool SupportsPermanentEntries => Source != "extra_expenses";
    public bool HasMonthSetupItems => MissingPermanentTemplates.Count > 0;
    public bool IsPermanent(string name) => PermanentTemplates.Any(x =>
        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    public DateTime MonthStart => new(Year, Month, 1);
    public DateTime PreviousMonth => MonthStart.AddMonths(-1);
    public DateTime NextMonth => MonthStart.AddMonths(1);
    public decimal Total => Rows.Sum(x => x.Amount);
    public decimal Average => Rows.Count == 0 ? 0 : Total / Rows.Count;
}
