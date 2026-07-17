using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class DashboardExperienceServiceTests
{
    private readonly DashboardExperienceService _service = new();

    [Fact]
    public void Build_CalculatesMonthlyMoneyJourneyFromExistingDashboardTotals()
    {
        var dashboard = new DashboardViewModel
        {
            MonthlyIncome = 3500m,
            CarryForwardAmount = 100m,
            BillsTotal = 1200m,
            ExpensesTotal = 350m,
            ExtraExpensesTotal = 150m,
            InvestmentsTotal = 300m,
            SavingsTotal = 450m
        };

        var result = _service.Build(dashboard);

        Assert.Equal(3600m, result.Journey.EffectiveIncome);
        Assert.Equal(1900m, result.Journey.AvailableBeforeFutureAllocations);
        Assert.Equal(750m, result.AllocatedToFuture);
        Assert.Equal(1150m, result.Journey.Remaining);
    }

    [Fact]
    public void Build_KeepsExtraExpensesSeparateFromEverydaySpending()
    {
        var dashboard = new DashboardViewModel
        {
            MonthlyIncome = 2000m,
            ExpensesTotal = 300m,
            ExtraExpensesTotal = 225m
        };

        var result = _service.Build(dashboard);

        Assert.Equal(300m, result.Journey.EverydaySpending);
        Assert.Equal(225m, result.Journey.ExtraExpenses);
    }

    [Fact]
    public void Build_CreatesRecentActivityFromRealMonthlyRows()
    {
        var dashboard = new DashboardViewModel
        {
            Bills =
            [
                new PaymentRow(1, "Mortgage", 900m, new DateTime(2026, 7, 1), null, null, null, null, "bills")
            ],
            Savings =
            [
                new PaymentRow(2, "Wedding", 250m, new DateTime(2026, 7, 10), null, null, null, null, "savings")
            ]
        };

        var result = _service.Build(dashboard);

        Assert.Equal(2, result.RecentActivity.Count);
        Assert.Equal("Wedding", result.RecentActivity[0].Title);
        Assert.Equal("Reserve allocation recorded", result.RecentActivity[0].Detail);
    }

    [Fact]
    public void Build_ReportsOverBudgetWhenFinalRemainingIsNegative()
    {
        var dashboard = new DashboardViewModel
        {
            MonthlyIncome = 1000m,
            BillsTotal = 1200m
        };

        var result = _service.Build(dashboard);

        Assert.True(result.Journey.IsOverBudget);
        Assert.Equal(-200m, result.Journey.Remaining);
    }
}
