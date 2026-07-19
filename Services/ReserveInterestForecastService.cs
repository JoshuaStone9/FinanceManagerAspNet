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
        var monthlyContribution = includeFutureContributions ? Math.Max(0m, account.MonthlyContribution) : 0m;
        var balance = openingBalance;
        var contributionTotal = 0m;
        var grossInterest = 0m;
        var estimatedTax = 0m;
        var cursor = startDate;

        while (cursor < forecastDate)
        {
            var next = cursor.AddMonths(1);
            if (next > forecastDate) next = forecastDate;
            var days = Math.Max(0d, (next - cursor).TotalDays);
            var grossForPeriod = balance * (decimal)(Math.Pow(1d + (double)(Math.Max(0m, account.InterestRate) / 100m), days / 365.2425d) - 1d);
            var taxable = account.TaxTreatment.Equals("Taxable", StringComparison.OrdinalIgnoreCase)
                && (!account.TaxEffectiveFrom.HasValue || next.Date >= account.TaxEffectiveFrom.Value.Date);
            var taxForPeriod = taxable ? grossForPeriod * (Math.Clamp(account.TaxRate, 0m, 100m) / 100m) : 0m;
            grossInterest += grossForPeriod;
            estimatedTax += taxForPeriod;
            balance += grossForPeriod - taxForPeriod;
            if (monthlyContribution > 0m && next < forecastDate.AddDays(1))
            {
                balance += monthlyContribution;
                contributionTotal += monthlyContribution;
            }
            cursor = next;
        }

        var netInterest = grossInterest - estimatedTax;
        return new ReserveAccountInterestForecast(account.Id, account.Name, openingBalance, account.InterestRate, monthlyContribution,
            Math.Round(contributionTotal,2), Math.Round(grossInterest,2), Math.Round(estimatedTax,2), Math.Round(netInterest,2), Math.Round(balance,2));
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
