using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class MonthlyCashFlowForecastServiceTests
{
    [Fact]
    public void Build_CarriesRemainingCashIntoFollowingMonth()
    {
        var service = new MonthlyCashFlowForecastService();
        var result = service.Build(new MonthlyCashFlowForecastRequest
        {
            StartMonth = new DateTime(2026, 7, 1), Months = 2, MonthlyIncome = 1000m,
            Bills = [Row("Rent", 800m, new DateTime(2026, 7, 1), "bills")]
        });

        Assert.Equal(200m, result.Months[0].RemainingCash);
        Assert.Equal(400m, result.Months[1].RemainingCash);
    }

    [Fact]
    public void Build_DoesNotRepeatOneOffPlannedExpense()
    {
        var service = new MonthlyCashFlowForecastService();
        var result = service.Build(new MonthlyCashFlowForecastRequest
        {
            StartMonth = new DateTime(2026, 7, 1), Months = 2, MonthlyIncome = 1000m,
            PlannedExpenses = [Row("Repair", 300m, new DateTime(2026, 7, 12), "extra_expenses")]
        });

        Assert.Equal(300m, result.Months[0].PlannedExpenses);
        Assert.Equal(0m, result.Months[1].PlannedExpenses);
    }

    private static PaymentRow Row(string name, decimal amount, DateTime date, string source)
        => new(1, name, amount, date, null, null, null, null, source);
}
