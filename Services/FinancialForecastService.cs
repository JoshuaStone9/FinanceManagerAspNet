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
        var potBalances = pots.ToDictionary(x => x.Id, x => x.AllocatedAmount + GetOneOff(request, x.Id));

        var month = new DateTime(startDate.Year, startDate.Month, 1).AddMonths(1);
        var endMonth = new DateTime(endDate.Year, endDate.Month, 1);
        decimal totalInterest = 0m;
        decimal totalContributions = 0m;
        decimal totalExpenses = 0m;

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

            var monthExpense = IsSameMonth(request.FutureExpenseDate, month)
                ? Math.Max(0m, request.FutureExpenseAmount)
                : 0m;
            if (monthExpense > 0m)
            {
                DeductExpense(accountStates, monthExpense);
                totalExpenses += monthExpense;
            }

            foreach (var pot in pots)
            {
                if (pot.IsPausedFor(month)) continue;
                var contribution = GetMonthlyContribution(request, pot);
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
                Math.Round(monthExpense, 2),
                Math.Round(closing, 2),
                baseline,
                Math.Round(allocated, 2),
                Math.Round(Math.Max(0m, closing - baseline - allocated), 2)));

            month = month.AddMonths(1);
        }

        var potResults = pots.Select(pot => BuildPotForecast(
            pot,
            startDate,
            endDate,
            GetOneOff(request, pot.Id),
            GetMonthlyContribution(request, pot))).ToList();
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
            ProjectedFutureExpenses = Math.Round(totalExpenses, 2),
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
                "Pot forecasts use recent real contribution history where available; legacy intended contribution values are only a compatibility fallback.",
                "Pot contributions are treated as virtual allocations within the household reserve, not additional cash leaving the reserve.",
                request.FutureExpenseAmount > 0m
                    ? "The selected future expense is deducted once from the reserve in its chosen month."
                    : "No additional future expense is included.",
                "The protected reserve baseline remains unavailable for pot allocation.",
                "Forecasts are advisory and do not change live balances, settings or historical records."
            }
        };
    }

    private static PotForecastResult BuildPotForecast(
        ReservePot pot,
        DateTime startDate,
        DateTime endDate,
        decimal oneOffContribution,
        decimal monthlyContribution)
    {
        var contribution = Math.Max(0m, monthlyContribution);
        var target = pot.TargetAmount;
        var current = pot.AllocatedAmount;
        var effectiveOpening = current + Math.Max(0m, oneOffContribution);
        var monthsInForecast = Math.Max(0, MonthDifference(startDate, endDate));
        var projected = effectiveOpening + (contribution * monthsInForecast);
        if (target.HasValue) projected = Math.Min(projected, target.Value);

        DateTime? completionDate = null;
        if (target.HasValue && effectiveOpening < target.Value && contribution > 0m)
        {
            var monthsRequired = (int)Math.Ceiling((target.Value - effectiveOpening) / contribution);
            completionDate = new DateTime(startDate.Year, startDate.Month, 1).AddMonths(monthsRequired);
        }
        else if (target.HasValue && effectiveOpening >= target.Value)
        {
            completionDate = startDate;
        }

        decimal? required = null;
        if (target.HasValue && pot.DueDate.HasValue && effectiveOpening < target.Value)
        {
            var monthsUntilDue = Math.Max(0, MonthDifference(startDate, pot.DueDate.Value));
            required = monthsUntilDue == 0
                ? target.Value - effectiveOpening
                : Math.Round((target.Value - effectiveOpening) / monthsUntilDue, 2);
        }

        var status = DetermineStatus(pot, effectiveOpening, startDate, completionDate, contribution);
        decimal? additional = required.HasValue
            ? Math.Round(Math.Max(0m, required.Value - contribution), 2)
            : null;
        int? monthsEarlyOrLate = completionDate.HasValue && pot.DueDate.HasValue
            ? SignedMonthDifference(pot.DueDate.Value, completionDate.Value)
            : null;

        return new PotForecastResult
        {
            PotId = pot.Id,
            PotName = pot.Name,
            CurrentBalance = current,
            TargetAmount = target,
            DueDate = pot.DueDate,
            IntendedMonthlyContribution = contribution,
            OneOffContribution = Math.Max(0m, oneOffContribution),
            ProjectedBalance = Math.Round(projected, 2),
            ProjectedCompletionDate = completionDate,
            RequiredMonthlyContribution = required,
            AdditionalMonthlyContributionRequired = additional,
            MonthsEarlyOrLate = monthsEarlyOrLate,
            Status = status,
            Explanation = BuildExplanation(status, pot, completionDate, additional, monthsEarlyOrLate),
            RecommendedAction = BuildRecommendation(status, pot, additional)
        };
    }

    private static ForecastGoalStatus DetermineStatus(
        ReservePot pot,
        decimal effectiveOpening,
        DateTime startDate,
        DateTime? completionDate,
        decimal monthlyContribution)
    {
        if (effectiveOpening < 0m) return ForecastGoalStatus.Overdrawn;
        if (!pot.TargetAmount.HasValue || pot.TargetAmount.Value <= 0m) return ForecastGoalStatus.NoTarget;
        if (effectiveOpening >= pot.TargetAmount.Value) return ForecastGoalStatus.Completed;
        if (!pot.DueDate.HasValue) return ForecastGoalStatus.NoDueDate;
        if (monthlyContribution <= 0m) return ForecastGoalStatus.NoContributionPlanned;
        if (pot.DueDate.Value.Date < startDate.Date) return ForecastGoalStatus.Behind;
        if (!completionDate.HasValue) return ForecastGoalStatus.Behind;
        if (completionDate.Value.Date <= pot.DueDate.Value.Date) return ForecastGoalStatus.OnTrack;
        if (completionDate.Value.Date <= pot.DueDate.Value.Date.AddMonths(2)) return ForecastGoalStatus.AtRisk;
        return ForecastGoalStatus.Behind;
    }

    private static string BuildExplanation(
        ForecastGoalStatus status,
        ReservePot pot,
        DateTime? completionDate,
        decimal? additional,
        int? monthsEarlyOrLate)
        => status switch
        {
            ForecastGoalStatus.Completed => "The target has already been reached.",
            ForecastGoalStatus.OnTrack when monthsEarlyOrLate < 0 => $"At the current contribution, this pot should finish {Math.Abs(monthsEarlyOrLate.Value)} month(s) before its due date.",
            ForecastGoalStatus.OnTrack => $"At the current contribution, this pot is projected to complete by {completionDate:MMMM yyyy}.",
            ForecastGoalStatus.AtRisk => $"This pot is projected to finish {Math.Max(1, monthsEarlyOrLate.GetValueOrDefault())} month(s) after its target date.",
            ForecastGoalStatus.Behind when monthsEarlyOrLate > 0 => $"This pot is projected to finish {monthsEarlyOrLate.Value} month(s) after its target date.",
            ForecastGoalStatus.Behind when additional.GetValueOrDefault() > 0m => $"The current contribution is insufficient. An additional {additional.Value:C} per month is required.",
            ForecastGoalStatus.Behind => "The target is overdue or cannot be reached with the current plan.",
            ForecastGoalStatus.NoContributionPlanned => "A target and date needed by exist, but there is not enough contribution history to estimate completion.",
            ForecastGoalStatus.NoTarget => "A completion forecast cannot be calculated until a target amount is set.",
            ForecastGoalStatus.NoDueDate => "A balance can be projected, but goal risk cannot be measured without a due date.",
            ForecastGoalStatus.Overdrawn => $"The pot is {Math.Abs(pot.AllocatedAmount):C} overdrawn and must first recover to £0.",
            _ => string.Empty
        };

    private static string BuildRecommendation(ForecastGoalStatus status, ReservePot pot, decimal? additional)
        => status switch
        {
            ForecastGoalStatus.Completed => "No action is required.",
            ForecastGoalStatus.OnTrack => "Continue with the current contribution.",
            ForecastGoalStatus.AtRisk or ForecastGoalStatus.Behind when additional.GetValueOrDefault() > 0m
                => $"Contributing an additional {additional.Value:C} per month would meet the date needed by.",
            ForecastGoalStatus.NoContributionPlanned when pot.TargetAmount.HasValue && pot.DueDate.HasValue
                => "Record contributions to build a contribution-driven completion estimate.",
            ForecastGoalStatus.NoTarget => "Set a target amount.",
            ForecastGoalStatus.NoDueDate => "Set a target date to enable risk analysis.",
            ForecastGoalStatus.Overdrawn => $"Restore {Math.Abs(pot.AllocatedAmount):C} before allocating towards the target.",
            _ => "Review the target, due date or planned contribution."
        };

    private static decimal GetMonthlyContribution(FinancialForecastRequest request, ReservePot pot)
        => request.PotMonthlyContributions.TryGetValue(pot.Id, out var amount)
            ? Math.Max(0m, amount)
            : Math.Max(0m, pot.IntendedMonthlyContribution);

    private static decimal GetOneOff(FinancialForecastRequest request, int potId)
        => request.PotOneOffContributions.TryGetValue(potId, out var amount) ? Math.Max(0m, amount) : 0m;

    private static bool IsSameMonth(DateTime? date, DateTime month)
        => date.HasValue && date.Value.Year == month.Year && date.Value.Month == month.Month;

    private static void DeductExpense(IDictionary<int, decimal> accountStates, decimal expense)
    {
        var remaining = expense;
        foreach (var accountId in accountStates.Keys.OrderBy(x => x).ToList())
        {
            if (remaining <= 0m) break;
            var deduction = Math.Min(accountStates[accountId], remaining);
            accountStates[accountId] -= deduction;
            remaining -= deduction;
        }
    }

    private static int MonthDifference(DateTime from, DateTime to)
    {
        var fromMonth = new DateTime(from.Year, from.Month, 1);
        var toMonth = new DateTime(to.Year, to.Month, 1);
        return Math.Max(0, ((toMonth.Year - fromMonth.Year) * 12) + toMonth.Month - fromMonth.Month);
    }

    private static int SignedMonthDifference(DateTime from, DateTime to)
    {
        var fromMonth = new DateTime(from.Year, from.Month, 1);
        var toMonth = new DateTime(to.Year, to.Month, 1);
        return ((toMonth.Year - fromMonth.Year) * 12) + toMonth.Month - fromMonth.Month;
    }
}
