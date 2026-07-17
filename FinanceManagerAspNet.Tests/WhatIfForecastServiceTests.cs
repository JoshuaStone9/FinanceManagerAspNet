using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class WhatIfForecastServiceTests
{
    [Fact]
    public void Build_AppliesTemporaryPotOverridesWithoutChangingLivePot()
    {
        var livePot = Pot();
        var service = new WhatIfForecastService(new FinancialForecastService());
        var input = new WhatIfForecastInput
        {
            Months = 12,
            PotId = livePot.Id,
            MonthlyContributionOverride = 200m,
            OneOffContribution = 300m
        };

        var result = service.Build(input, Summary(), [livePot]);

        Assert.Equal(100m, result.CurrentPot!.IntendedMonthlyContribution);
        Assert.Equal(200m, result.ScenarioPot!.IntendedMonthlyContribution);
        Assert.Equal(300m, result.ScenarioPot.OneOffContribution);
        Assert.Equal(100m, livePot.IntendedMonthlyContribution);
        Assert.Equal(0m, livePot.AllocatedAmount);
    }

    [Fact]
    public void Build_InterestOverrideOnlyChangesScenarioAccounts()
    {
        var service = new WhatIfForecastService(new FinancialForecastService());
        var result = service.Build(
            new WhatIfForecastInput { Months = 12, InterestRateOverride = 12m },
            Summary(),
            [Pot()]);

        Assert.True(result.ScenarioPlan.ProjectedInterest > result.CurrentPlan.ProjectedInterest);
        Assert.Equal(0m, Summary().SelectedAccounts.Single().InterestRate);
    }

    private static HouseholdReserveAccountSummary Summary() => new()
    {
        Baseline = 12000m,
        SelectedAccounts = [new ReserveAccountOption(1, "Reserve", 12000m, 0m, 0m, true)]
    };

    private static ReservePot Pot() => new(
        1, "Holiday", 0m, 100m, 100m, "Monthly", null, true, true,
        null, null, null, DateTime.Today, 1200m, DateTime.Today.AddMonths(12),
        1, true, null, DateTime.Today);
}
