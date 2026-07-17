using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IMonthlyCashFlowForecastService
{
    MonthlyCashFlowForecastResult Build(MonthlyCashFlowForecastRequest request);
}

public sealed class MonthlyCashFlowForecastService : IMonthlyCashFlowForecastService
{
    public MonthlyCashFlowForecastResult Build(MonthlyCashFlowForecastRequest request)
    {
        var start = new DateTime(request.StartMonth.Year, request.StartMonth.Month, 1);
        var monthCount = Math.Clamp(request.Months, 1, 36);
        var rows = new List<MonthlyCashFlowForecastRow>(monthCount);
        var cashCarry = request.OpeningCarryForward;
        var reserveBalance = Math.Max(0m, request.OpeningReserveBalance);

        for (var index = 0; index < monthCount; index++)
        {
            var month = start.AddMonths(index);
            var bills = RecurringTotal(request.Bills, month);
            var everyday = RecurringTotal(request.EverydaySpending, month);
            var investments = RecurringTotal(request.Investments, month);
            var reserveContributions = RecurringTotal(request.ReserveAllocations, month);
            var plannedExpenses = request.PlannedExpenses
                .Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month)
                .Sum(x => x.Amount);
            var adjustments = request.Adjustments
                .Where(x => x.Month.Year == month.Year && x.Month.Month == month.Month)
                .Sum(x => x.Amount);

            var openingCash = cashCarry;
            var remaining = request.MonthlyIncome + openingCash + adjustments
                - bills - everyday - investments - reserveContributions - plannedExpenses;
            reserveBalance += reserveContributions;
            var interest = reserveBalance * Math.Max(0m, request.AnnualReserveInterestRate) / 100m / 12m;
            reserveBalance += interest;

            rows.Add(new MonthlyCashFlowForecastRow
            {
                Month = month,
                Income = Round(request.MonthlyIncome),
                Bills = Round(bills),
                EverydaySpending = Round(everyday),
                Investments = Round(investments),
                HouseholdReserveContributions = Round(reserveContributions),
                PlannedExpenses = Round(plannedExpenses),
                Adjustments = Round(adjustments),
                InterestEarned = Round(interest),
                OpeningCash = Round(openingCash),
                RemainingCash = Round(remaining),
                ClosingReserveBalance = Round(reserveBalance),
                IsCurrentMonth = index == 0
            });

            cashCarry = remaining;
        }

        return new MonthlyCashFlowForecastResult { Months = rows };
    }

    private static decimal RecurringTotal(IEnumerable<PaymentRow> rows, DateTime month)
        => rows.Where(row => IsActive(row, month)).Sum(row => row.Amount);

    private static bool IsActive(PaymentRow row, DateTime month)
    {
        var rowMonth = new DateTime(row.Date.Year, row.Date.Month, 1);
        if (month < rowMonth) return false;
        if (!row.LengthMonths.HasValue) return true;
        return month < rowMonth.AddMonths(Math.Max(1, row.LengthMonths.Value));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
