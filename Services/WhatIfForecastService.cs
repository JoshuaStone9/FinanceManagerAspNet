using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IWhatIfForecastService
{
    WhatIfForecastViewModel Build(
        WhatIfForecastInput input,
        HouseholdReserveAccountSummary accountSummary,
        IReadOnlyList<ReservePot> livePots);
}

public sealed class WhatIfForecastService(IFinancialForecastService financialForecastService) : IWhatIfForecastService
{
    public WhatIfForecastViewModel Build(
        WhatIfForecastInput input,
        HouseholdReserveAccountSummary accountSummary,
        IReadOnlyList<ReservePot> livePots)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(accountSummary);
        ArgumentNullException.ThrowIfNull(livePots);

        var months = new[] { 3, 6, 12, 24 }.Contains(input.Months) ? input.Months : 12;
        var startDate = DateTime.Today;
        var endDate = startDate.AddMonths(months);

        var currentRequest = new FinancialForecastRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            ProtectedReserveBaseline = accountSummary.Baseline,
            Accounts = accountSummary.SelectedAccounts,
            Pots = livePots
        };

        var scenarioAccounts = accountSummary.SelectedAccounts
            .Select(account => input.InterestRateOverride.HasValue
                ? account with { InterestRate = Math.Max(0m, input.InterestRateOverride.Value) }
                : account)
            .ToList();

        var scenarioPots = livePots.Select(pot =>
        {
            if (!input.PotId.HasValue || pot.Id != input.PotId.Value) return pot;
            return pot with
            {
                IntendedMonthlyContribution = input.MonthlyContributionOverride.HasValue
                    ? Math.Max(0m, input.MonthlyContributionOverride.Value)
                    : pot.IntendedMonthlyContribution,
                TargetAmount = input.TargetAmountOverride.HasValue
                    ? Math.Max(0m, input.TargetAmountOverride.Value)
                    : pot.TargetAmount,
                DueDate = input.TargetDateOverride ?? pot.DueDate
            };
        }).ToList();

        var oneOffs = input.PotId.HasValue && input.OneOffContribution > 0m
            ? new Dictionary<int, decimal> { [input.PotId.Value] = input.OneOffContribution }
            : new Dictionary<int, decimal>();

        var scenarioRequest = new FinancialForecastRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            ProtectedReserveBaseline = input.ProtectedBaselineOverride.HasValue
                ? Math.Max(0m, input.ProtectedBaselineOverride.Value)
                : accountSummary.Baseline,
            Accounts = scenarioAccounts,
            Pots = scenarioPots,
            FutureExpenseAmount = Math.Max(0m, input.FutureExpenseAmount),
            FutureExpenseDate = input.FutureExpenseDate,
            PotOneOffContributions = oneOffs
        };

        var current = financialForecastService.Build(currentRequest);
        var scenario = financialForecastService.Build(scenarioRequest);

        return new WhatIfForecastViewModel
        {
            Input = input,
            AvailablePots = livePots.Where(x => x.IsActive).OrderBy(x => x.Priority).ThenBy(x => x.Name).ToList(),
            CurrentPlan = current,
            ScenarioPlan = scenario,
            CurrentPot = input.PotId.HasValue ? current.Pots.FirstOrDefault(x => x.PotId == input.PotId.Value) : null,
            ScenarioPot = input.PotId.HasValue ? scenario.Pots.FirstOrDefault(x => x.PotId == input.PotId.Value) : null,
            HasScenario = HasAnyOverride(input)
        };
    }

    private static bool HasAnyOverride(WhatIfForecastInput input)
        => input.PotId.HasValue
           || input.MonthlyContributionOverride.HasValue
           || input.TargetAmountOverride.HasValue
           || input.TargetDateOverride.HasValue
           || input.OneOffContribution > 0m
           || input.InterestRateOverride.HasValue
           || input.ProtectedBaselineOverride.HasValue
           || input.FutureExpenseAmount > 0m;
}
