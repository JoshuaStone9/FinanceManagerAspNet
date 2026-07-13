using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public sealed record FundingEngineInput(
    ReservePot Pot,
    DateTime PeriodStart,
    DateTime AsOfDate,
    decimal DashboardContribution,
    decimal OutstandingRecovery,
    decimal CarriedExcessBalance,
    bool IsCurrentPeriod);

public sealed record FundingEngineResult(
    decimal ExpectedAmount,
    decimal ActualAmount,
    decimal AppliedToCurrentMonth,
    decimal AppliedToRecovery,
    decimal CarriedExcessUsed,
    decimal CarriedExcessCreated,
    decimal ShortfallAmount,
    decimal GenuineExcess,
    decimal RecoveryBalance,
    decimal CarriedExcessBalance,
    string Status,
    bool IsPaused)
{
    public decimal EffectiveCurrentMonthFunding => AppliedToCurrentMonth + CarriedExcessUsed;
}

public interface IFundingEngineService
{
    FundingEngineResult Calculate(FundingEngineInput input);
}

/// <summary>
/// Pure Phase 2.1/2.2 funding engine. It has no database or UI dependencies, so all
/// reserve-pot pages can use the same deterministic status and allocation rules.
/// </summary>
public sealed class FundingEngineService : IFundingEngineService
{
    public FundingEngineResult Calculate(FundingEngineInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Pot);

        var pot = input.Pot;
        var periodStart = new DateTime(input.PeriodStart.Year, input.PeriodStart.Month, 1);
        var actual = Math.Max(0m, input.DashboardContribution);
        var recovery = Math.Max(0m, input.OutstandingRecovery);
        var carriedExcess = Math.Max(0m, input.CarriedExcessBalance);

        // The current schema stores a single pause-until date rather than historical pause ranges.
        // Therefore a pause is applied to the current period only. Historical pause periods can be
        // introduced later without changing the contribution allocation order implemented here.
        var paused = pot.IsActive
            && input.IsCurrentPeriod
            && pot.FundingPausedUntil.HasValue
            && pot.FundingPausedUntil.Value.Date >= input.AsOfDate.Date;

        var expected = !pot.IsActive || paused ||
                       pot.FundingFrequency.Equals("Irregular", StringComparison.OrdinalIgnoreCase)
            ? 0m
            : Math.Max(0m, pot.IntendedMonthlyContribution);

        var available = actual;
        decimal appliedToCurrent = 0m;
        decimal appliedToRecovery = 0m;
        decimal carriedExcessUsed = 0m;
        decimal carriedExcessCreated = 0m;
        decimal genuineExcess = 0m;
        decimal shortfall = 0m;

        if (!pot.IsActive)
        {
            // Keep any dashboard money visible but do not use it to satisfy an inactive plan.
            genuineExcess = available;
        }
        else if (paused)
        {
            // Smart pause: no current-period expectation, but voluntary contributions remain valid.
            appliedToRecovery = Math.Min(available, recovery);
            recovery -= appliedToRecovery;
            available -= appliedToRecovery;

            if (pot.CarryExcessForward)
            {
                carriedExcessCreated = available;
                carriedExcess += carriedExcessCreated;
            }
            else
            {
                genuineExcess = available;
            }
        }
        else if (expected > 0m)
        {
            // Phase 2.2 allocation order:
            // 1) carried credit/current dashboard money satisfy this period,
            // 2) remaining dashboard money clears oldest recovery,
            // 3) remaining money becomes carried credit (when enabled) or genuine excess.
            carriedExcessUsed = Math.Min(carriedExcess, expected);
            carriedExcess -= carriedExcessUsed;

            var currentStillRequired = Math.Max(0m, expected - carriedExcessUsed);
            appliedToCurrent = Math.Min(available, currentStillRequired);
            available -= appliedToCurrent;

            appliedToRecovery = Math.Min(available, recovery);
            recovery -= appliedToRecovery;
            available -= appliedToRecovery;

            shortfall = Math.Max(0m, expected - appliedToCurrent - carriedExcessUsed);
            if (pot.CarryForwardShortfalls)
            {
                recovery += shortfall;
            }

            if (pot.CarryExcessForward)
            {
                carriedExcessCreated = available;
                carriedExcess += carriedExcessCreated;
            }
            else
            {
                genuineExcess = available;
            }
        }
        else
        {
            // A configured irregular/no-expectation month can still receive money, but it is not
            // classified as scheduled funding.
            genuineExcess = available;
        }

        var effectiveFunding = appliedToCurrent + carriedExcessUsed;
        var status = CalculateStatus(
            pot,
            periodStart,
            input.AsOfDate,
            input.IsCurrentPeriod,
            paused,
            expected,
            actual,
            effectiveFunding,
            carriedExcessUsed,
            carriedExcessCreated,
            genuineExcess);

        return new FundingEngineResult(
            ExpectedAmount: expected,
            ActualAmount: actual,
            AppliedToCurrentMonth: appliedToCurrent,
            AppliedToRecovery: appliedToRecovery,
            CarriedExcessUsed: carriedExcessUsed,
            CarriedExcessCreated: carriedExcessCreated,
            ShortfallAmount: shortfall,
            GenuineExcess: genuineExcess,
            RecoveryBalance: recovery,
            CarriedExcessBalance: carriedExcess,
            Status: status,
            IsPaused: paused);
    }

    private static string CalculateStatus(
        ReservePot pot,
        DateTime periodStart,
        DateTime asOfDate,
        bool isCurrentPeriod,
        bool paused,
        decimal expected,
        decimal actual,
        decimal effectiveFunding,
        decimal carriedExcessUsed,
        decimal carriedExcessCreated,
        decimal genuineExcess)
    {
        if (!pot.IsActive)
            return "Inactive";

        if (paused)
            return actual > 0m ? "Paused – voluntary contribution" : "Paused";

        if (expected <= 0m)
            return "Not configured";

        if (effectiveFunding >= expected)
        {
            if (actual <= 0m && carriedExcessUsed > 0m)
                return "Funded from carried excess";

            return carriedExcessCreated > 0m || genuineExcess > 0m
                ? "Overfunded"
                : "Funded";
        }

        if (!isCurrentPeriod)
            return effectiveFunding > 0m ? "Partially funded" : "Missed";

        var lastDay = DateTime.DaysInMonth(periodStart.Year, periodStart.Month);
        var dueDay = Math.Min(pot.ExpectedFundingDay ?? lastDay, lastDay);
        var dueDate = new DateTime(periodStart.Year, periodStart.Month, dueDay);

        if (effectiveFunding > 0m)
            return asOfDate.Date > dueDate ? "Partially funded" : "In progress";

        return asOfDate.Date > dueDate ? "Overdue" : "Pending";
    }
}
