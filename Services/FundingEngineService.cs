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

        // Smart pause uses an explicit date range. A paused month has no scheduled
        // expectation, but voluntary contributions can still clear old recovery and
        // create future credit or genuine excess. A month is paused when any day in
        // that month overlaps the configured pause range.
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var pauseFrom = (pot.FundingPausedFrom ?? pot.FundingPausedUntil)?.Date;
        var pauseUntil = pot.FundingPausedUntil?.Date;
        var paused = pot.IsActive
            && pauseFrom.HasValue
            && pauseUntil.HasValue
            && pauseFrom.Value <= periodEnd
            && pauseUntil.Value >= periodStart;

        // Funding is flexible within the month. There is no frequency or expected-day rule;
        // the engine only assesses the total contributed during the monthly period.
        var expected = !pot.IsActive || paused
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

        // Contributions can be made on any day. A current month does not become overdue
        // until it has ended; historic periods are handled above as Missed/Partially funded.
        if (effectiveFunding > 0m)
            return "In progress";

        return "Pending";
    }
}
