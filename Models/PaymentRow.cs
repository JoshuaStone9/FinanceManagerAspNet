namespace FinanceManagerAspNet.Models;

public record PaymentRow(
    int Id,
    string Name,
    decimal Amount,
    DateTime Date,
    string? Category,
    string? Type,
    string? Length,
    string? Notes,
    string Source)
{
    public int? LengthMonths =>
        int.TryParse(Length, out var months) ? months : null;

    public DateTime? EndDate =>
        LengthMonths.HasValue ? Date.AddMonths(LengthMonths.Value) : null;

    public int? MonthsRemaining => LengthMonths;

    public int? ProgressPercent
    {
        get
        {
            if (!LengthMonths.HasValue || LengthMonths.Value <= 0)
                return null;

            return Math.Clamp(100 - (LengthMonths.Value * 100 / GetEstimatedOriginalLength()), 0, 100);
        }
    }

    private int GetEstimatedOriginalLength()
    {
        if (LengthMonths is null or <= 0)
            return 1;

        return LengthMonths.Value switch
        {
            <= 12 => 12,
            <= 24 => 24,
            <= 36 => 36,
            <= 48 => 48,
            <= 60 => 60,
            <= 120 => 120,
            <= 240 => 240,
            <= 300 => 300,
            <= 420 => 420,
            _ => LengthMonths.Value
        };
    }
}