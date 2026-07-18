using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;

namespace FinanceManagerAspNet.Tests;

public sealed class MonthlyFinancialHealthServiceTests
{
    private readonly MonthlyFinancialHealthService _service = new();

    [Fact]
    public void Build_ReturnsHealthySummary_WhenMonthHasSurplusAndFutureAllocations()
    {
        var result = _service.Build(
            new MonthlyFinancialSnapshot(3500m, 0m, 1000m, 400m, 100m, 300m, 300m),
            null);

        Assert.Equal("Healthy", result.Status);
        Assert.Equal(1400m, result.Remaining);
        Assert.Equal(60m, result.CommittedPercent);
        Assert.Contains(result.Insights, x => x.Title == "Month currently in surplus");
    }

    [Fact]
    public void Build_ReturnsNeedsAttention_WhenAllocationsExceedIncome()
    {
        var result = _service.Build(
            new MonthlyFinancialSnapshot(2000m, 0m, 1200m, 600m, 300m, 100m, 100m),
            null);

        Assert.Equal("Needs attention", result.Status);
        Assert.Equal(-300m, result.Remaining);
        Assert.Contains(result.Insights, x => x.Severity == "danger");
    }

    [Fact]
    public void Build_ReportsMonthOnMonthEverydaySpendingChange()
    {
        var result = _service.Build(
            new MonthlyFinancialSnapshot(3000m, 0m, 1000m, 500m, 0m, 0m, 0m),
            new MonthlyFinancialSnapshot(3000m, 0m, 1000m, 350m, 0m, 0m, 0m));

        Assert.Equal(150m, result.EverydaySpendingChange);
        Assert.Contains(result.Insights, x => x.Title == "Everyday spending increased");
    }
}
