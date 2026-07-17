using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class ForecastRecommendationServiceTests
{
    [Fact]
    public void Build_ReturnsCriticalRecommendationForNegativeMonth()
    {
        var service = new ForecastRecommendationService();
        var cashFlow = new MonthlyCashFlowForecastResult
        {
            Months = [new MonthlyCashFlowForecastRow { Month = new DateTime(2026, 8, 1), RemainingCash = -125m }]
        };

        var result = service.Build(cashFlow, new FinancialForecastResult());

        Assert.Contains(result, x => x.Type == ForecastRecommendationType.CashShortfall && x.Severity == ForecastRecommendationSeverity.Critical);
    }
}
