using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IFinancialForecastService
{
    FinancialForecastResult Build(FinancialForecastRequest request);
}

public sealed class FinancialForecastService : IFinancialForecastService
{
    public FinancialForecastResult Build(FinancialForecastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date < startDate ? startDate : request.EndDate.Date;
        var baseline = Math.Max(0m, request.ProtectedReserveBaseline);
        var accounts = request.Accounts.Where(x => x.IsSelected).ToList();
        var pots = request.Pots.Where(x => x.IsActive).OrderBy(x => x.Priority).ThenBy(x => x.Name).ToList();

        var openingReserve = accounts.Sum(x => Math.Max(0m, x.Balance));
        var accountStates = accounts.ToDictionary(x => x.Id, x => Math.Max(0m, x.Balance));
        var monthlyRows = new List<MonthlyForecastRow>();
        var potBalances = pots.ToDictionary(x => x.Id, x => x.AllocatedAmount);

        var month = new DateTime(startDate.Year, startDate.Month, 1).AddMonths(1);
        var endMonth = new DateTime(endDate.Year, endDate.Month, 1);
        decimal totalInterest = 0m;
        decimal totalContributions = 0m;

        while (month <= endMonth)
        {
            var opening = accountStates.Values.Sum();
            decimal monthContributions = 0m;
            decimal monthInterest = 0m;

            foreach (var account in accounts)
            {
                var contribution = request.IncludeAccountContributions ? Math.Max(0m, account.MonthlyContribution) : 0m;
                var rate = Math.Max(0m, account.InterestRate) / 100m / 12m;
                var current = accountStates[account.Id] + contribution;
                var interest = current * rate;
                accountStates[account.Id] = current + interest;
                monthContributions += contribution;
                monthInterest += interest;
            }

            foreach (var pot in pots)
            {
                if (pot.IsPausedFor(month)) continue;
                var contribution = Math.Max(0m, pot.IntendedMonthlyContribution);
                var projected = potBalances[pot.Id] + contribution;
                potBalances[pot.Id] = pot.TargetAmount.HasValue
                    ? Math.Min(projected, pot.TargetAmount.Value)
                    : projected;
            }

            totalContributions += monthContributions;
            totalInterest += monthInterest;
            var closing = accountStates.Values.Sum();
            var allocated = potBalances.Values.Sum(x => Math.Max(0m, x));
            monthlyRows.Add(new MonthlyForecastRow(
                month,
                Math.Round(opening, 2),
                Math.Round(monthContributions, 2),
                Math.Round(monthInterest, 2),
                Math.Round(closing, 2),
                baseline,
                Math.Round(allocated, 2),
                Math.Round(Math.Max(0m, closing - baseline - allocated), 2)));

            month = month.AddMonths(1);
        }

        var potResults = pots.Select(pot => BuildPotForecast(pot, startDate, endDate)).ToList();
        var projectedReserve = accountStates.Values.Sum();
        var projectedAllocated = potResults.Sum(x => Math.Max(0m, x.ProjectedBalance));
        var surplus = Math.Max(0m, projectedReserve - baseline);

        return new FinancialForecastResult
        {
            StartDate = startDate,
            EndDate = endDate,
            ProtectedReserveBaseline = baseline,
            OpeningReserveBalance = Math.Round(openingReserve, 2),
            ProjectedReserveBalance = Math.Round(projectedReserve, 2),
            ProjectedInterest = Math.Round(totalInterest, 2),
            ProjectedAccountContributions = Math.Round(totalContributions, 2),
            ProjectedAllocatedToPots = Math.Round(projectedAllocated, 2),
            ProjectedSurplusAboveBaseline = Math.Round(surplus, 2),
            ProjectedUnallocatedSurplus = Math.Round(Math.Max(0m, surplus - projectedAllocated), 2),
            Months = monthlyRows,
            Pots = potResults,
            Assumptions = new[]
            {
                "The forecast starts from the balances currently held in the selected reserve accounts.",
                "Interest is estimated monthly using each selected account's annual interest rate.",
                request.IncludeAccountContributions
                    ? "Configured monthly reserve-account contributions are included."
                    : "Future reserve-account contributions are excluded.",
                "Pot contributions are treated as virtual allocations within the household reserve, not additional cash leaving the reserve.",
                "The protected reserve baseline remains unavailable for pot allocation.",
                "Forecasts are advisory and do not change live balances, settings or historical records."
            }
        };
    }

    private static PotForecastResult BuildPotForecast(ReservePot pot, DateTime startDate, DateTime endDate)
    {
        var contribution = Math.Max(0m, pot.IntendedMonthlyContribution);
        var target = pot.TargetAmount;
        var current = pot.AllocatedAmount;
        var monthsInForecast = Math.Max(0, MonthDifference(startDate, endDate));
        var projected = current + (contribution * monthsInForecast);
        if (target.HasValue) projected = Math.Min(projected, target.Value);

        DateTime? completionDate = null;
        if (target.HasValue && current < target.Value && contribution > 0m)
        {
            var monthsRequired = (int)Math.Ceiling((target.Value - current) / contribution);
            completionDate = new DateTime(startDate.Year, startDate.Month, 1).AddMonths(monthsRequired);
        }
        else if (target.HasValue && current >= target.Value)
        {
            completionDate = startDate;
        }

        decimal? required = null;
        if (target.HasValue && pot.DueDate.HasValue && current < target.Value)
        {
            var monthsUntilDue = Math.Max(0, MonthDifference(startDate, pot.DueDate.Value));
            required = monthsUntilDue == 0
                ? target.Value - current
                : Math.Round((target.Value - current) / monthsUntilDue, 2);
        }

        var status = DetermineStatus(pot, startDate, completionDate);
        decimal? additional = required.HasValue ? Math.Round(Math.Max(0m, required.Value - contribution), 2) : null;

        return new PotForecastResult
        {
            PotId = pot.Id,
            PotName = pot.Name,
            CurrentBalance = current,
            TargetAmount = target,
            DueDate = pot.DueDate,
            IntendedMonthlyContribution = contribution,
            ProjectedBalance = Math.Round(projected, 2),
            ProjectedCompletionDate = completionDate,
            RequiredMonthlyContribution = required,
            AdditionalMonthlyContributionRequired = additional,
            Status = status,
            Explanation = BuildExplanation(status, pot, completionDate, additional)
        };
    }

    private static ForecastGoalStatus DetermineStatus(ReservePot pot, DateTime startDate, DateTime? completionDate)
    {
        if (pot.AllocatedAmount < 0m) return ForecastGoalStatus.Overdrawn;
        if (!pot.TargetAmount.HasValue || pot.TargetAmount.Value <= 0m) return ForecastGoalStatus.NoTarget;
        if (pot.AllocatedAmount >= pot.TargetAmount.Value) return ForecastGoalStatus.Completed;
        if (!pot.DueDate.HasValue) return ForecastGoalStatus.NoDueDate;
        if (pot.IntendedMonthlyContribution <= 0m) return ForecastGoalStatus.NoContributionPlanned;
        if (pot.DueDate.Value.Date < startDate.Date) return ForecastGoalStatus.Behind;
        if (!completionDate.HasValue) return ForecastGoalStatus.Behind;
        if (completionDate.Value.Date <= pot.DueDate.Value.Date) return ForecastGoalStatus.OnTrack;
        if (completionDate.Value.Date <= pot.DueDate.Value.Date.AddMonths(2)) return ForecastGoalStatus.AtRisk;
        return ForecastGoalStatus.Behind;
    }

    private static string BuildExplanation(ForecastGoalStatus status, ReservePot pot, DateTime? completionDate, decimal? additional)
        => status switch
        {
            ForecastGoalStatus.Completed => "The target has already been reached.",
            ForecastGoalStatus.OnTrack => $"At the current intended contribution, this pot is projected to complete by {completionDate:MMMM yyyy}.",
            ForecastGoalStatus.AtRisk => $"This pot is projected to complete shortly after its due date. Add approximately {additional.GetValueOrDefault():C} per month to bring it back on track.",
            ForecastGoalStatus.Behind => additional.GetValueOrDefault() > 0m
                ? $"The current contribution is insufficient for the due date. Add approximately {additional.Value:C} per month."
                : "The target is already overdue or cannot be reached with the current plan.",
            ForecastGoalStatus.NoContributionPlanned => "A target and due date exist, but no intended monthly contribution is planned.",
            ForecastGoalStatus.NoTarget => "Add a target amount before a completion forecast can be calculated.",
            ForecastGoalStatus.NoDueDate => "The pot can be projected forward, but a risk status requires a due date.",
            ForecastGoalStatus.Overdrawn => $"Restore the negative balance of {Math.Abs(pot.AllocatedAmount):C} before progressing towards the target.",
            _ => string.Empty
        };

    private static int MonthDifference(DateTime from, DateTime to)
    {
        var fromMonth = new DateTime(from.Year, from.Month, 1);
        var toMonth = new DateTime(to.Year, to.Month, 1);
        return Math.Max(0, ((toMonth.Year - fromMonth.Year) * 12) + toMonth.Month - fromMonth.Month);
    }
}
