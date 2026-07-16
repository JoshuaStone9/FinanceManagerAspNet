using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IReserveInterestForecastService
{
    ReserveInterestForecast Build(
        IReadOnlyList<ReserveAccountOption> accounts,
        DateTime forecastDate,
        bool includeFutureContributions);
}

public sealed class ReserveInterestForecastService : IReserveInterestForecastService
{
    public ReserveInterestForecast Build(
        IReadOnlyList<ReserveAccountOption> accounts,
        DateTime forecastDate,
        bool includeFutureContributions)
    {
        var startDate = DateTime.Today;
        var safeForecastDate = forecastDate.Date < startDate
            ? startDate
            : forecastDate.Date;

        var accountForecasts = accounts
            .Select(account => BuildAccountForecast(
                account,
                startDate,
                safeForecastDate,
                includeFutureContributions))
            .ToList();

        return new ReserveInterestForecast
        {
            StartDate = startDate,
            ForecastDate = safeForecastDate,
            IncludeFutureContributions = includeFutureContributions,
            Accounts = accountForecasts
        };
    }

    private static ReserveAccountInterestForecast BuildAccountForecast(
        ReserveAccountOption account,
        DateTime startDate,
        DateTime forecastDate,
        bool includeFutureContributions)
    {
        var openingBalance = Math.Max(0m, account.Balance);
        var annualRate = Math.Max(0m, account.InterestRate) / 100m;
        var monthlyContribution = includeFutureContributions
            ? Math.Max(0m, account.MonthlyContribution)
            : 0m;

        var projectedOpeningBalance = Grow(openingBalance, annualRate, startDate, forecastDate);
        var contributionTotal = 0m;
        var projectedContributionValue = 0m;

        if (monthlyContribution > 0m && forecastDate > startDate)
        {
            var contributionDate = startDate.AddMonths(1);
            while (contributionDate <= forecastDate)
            {
                contributionTotal += monthlyContribution;
                projectedContributionValue += Grow(
                    monthlyContribution,
                    annualRate,
                    contributionDate,
                    forecastDate);

                contributionDate = contributionDate.AddMonths(1);
            }
        }

        var projectedTotal = projectedOpeningBalance + projectedContributionValue;
        var interestEarned = projectedTotal - openingBalance - contributionTotal;

        return new ReserveAccountInterestForecast(
            account.Id,
            account.Name,
            openingBalance,
            account.InterestRate,
            monthlyContribution,
            Math.Round(contributionTotal, 2),
            Math.Round(Math.Max(0m, interestEarned), 2),
            Math.Round(projectedTotal, 2));
    }

    private static decimal Grow(
        decimal amount,
        decimal annualRate,
        DateTime fromDate,
        DateTime toDate)
    {
        if (amount <= 0m || annualRate <= 0m || toDate <= fromDate)
        {
            return amount;
        }

        var days = (toDate.Date - fromDate.Date).TotalDays;
        var growthFactor = Math.Pow(1d + (double)annualRate, days / 365.2425d);
        return amount * (decimal)growthFactor;
    }
}
