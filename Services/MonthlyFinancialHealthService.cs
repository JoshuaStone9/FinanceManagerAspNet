using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IMonthlyFinancialHealthService
{
    MonthlyFinancialHealthSummary Build(
        MonthlyFinancialSnapshot current,
        MonthlyFinancialSnapshot? previous);
}

public sealed class MonthlyFinancialHealthService : IMonthlyFinancialHealthService
{
    public MonthlyFinancialHealthSummary Build(
        MonthlyFinancialSnapshot current,
        MonthlyFinancialSnapshot? previous)
    {
        ArgumentNullException.ThrowIfNull(current);

        var effectiveIncome = current.Income + current.CarryForward;
        var coreSpending = current.Bills + current.EverydaySpending + current.ExtraExpenses;
        var futureAllocations = current.Investments + current.MoneyPots;
        var totalAllocated = coreSpending + futureAllocations;
        var remaining = effectiveIncome - totalAllocated;
        var committedPercent = effectiveIncome > 0
            ? Math.Round((totalAllocated / effectiveIncome) * 100m, 1)
            : 0m;
        var billsPercent = effectiveIncome > 0
            ? Math.Round((current.Bills / effectiveIncome) * 100m, 1)
            : 0m;
        var futurePercent = effectiveIncome > 0
            ? Math.Round((futureAllocations / effectiveIncome) * 100m, 1)
            : 0m;

        var previousRemaining = previous is null
            ? (decimal?)null
            : previous.Income + previous.CarryForward
                - previous.Bills
                - previous.EverydaySpending
                - previous.ExtraExpenses
                - previous.Investments
                - previous.MoneyPots;

        var insights = BuildInsights(
            current,
            previous,
            effectiveIncome,
            remaining,
            billsPercent,
            futureAllocations,
            committedPercent);

        return new MonthlyFinancialHealthSummary
        {
            Status = remaining < 0
                ? "Needs attention"
                : committedPercent >= 90m
                    ? "Tight month"
                    : futurePercent >= 10m
                        ? "Healthy"
                        : "Stable",
            StatusCssClass = remaining < 0
                ? "danger"
                : committedPercent >= 90m
                    ? "warning"
                    : "positive",
            EffectiveIncome = effectiveIncome,
            CoreSpending = coreSpending,
            FutureAllocations = futureAllocations,
            Remaining = remaining,
            CommittedPercent = committedPercent,
            EssentialBillsPercent = billsPercent,
            FutureAllocationPercent = futurePercent,
            PreviousRemaining = previousRemaining,
            RemainingChange = previousRemaining.HasValue
                ? remaining - previousRemaining.Value
                : null,
            EverydaySpendingChange = previous is null
                ? null
                : current.EverydaySpending - previous.EverydaySpending,
            ExtraExpensesChange = previous is null
                ? null
                : current.ExtraExpenses - previous.ExtraExpenses,
            Insights = insights
        };
    }

    private static IReadOnlyList<FinancialHealthInsight> BuildInsights(
        MonthlyFinancialSnapshot current,
        MonthlyFinancialSnapshot? previous,
        decimal effectiveIncome,
        decimal remaining,
        decimal billsPercent,
        decimal futureAllocations,
        decimal committedPercent)
    {
        var insights = new List<FinancialHealthInsight>();

        if (effectiveIncome <= 0)
        {
            insights.Add(new(
                "Income needed",
                "Add this month's income to unlock an accurate financial health summary.",
                "warning",
                "wallet-cards"));
            return insights;
        }

        insights.Add(remaining >= 0
            ? new FinancialHealthInsight(
                "Month currently in surplus",
                $"You have {remaining:C} remaining after all recorded spending and allocations.",
                "positive",
                "circle-check-big")
            : new FinancialHealthInsight(
                "Month currently in shortfall",
                $"Recorded spending and allocations exceed available income by {Math.Abs(remaining):C}.",
                "danger",
                "triangle-alert"));

        insights.Add(new FinancialHealthInsight(
            "Essential commitments",
            $"Essential bills use {billsPercent:0.#}% of available monthly income.",
            billsPercent >= 60m ? "warning" : "neutral",
            "receipt"));

        if (futureAllocations > 0)
        {
            insights.Add(new FinancialHealthInsight(
                "Building for the future",
                $"You have allocated {futureAllocations:C} to investments and Money Pots this month.",
                "positive",
                "trending-up"));
        }

        if (previous is not null)
        {
            var everydayChange = current.EverydaySpending - previous.EverydaySpending;
            if (everydayChange != 0)
            {
                insights.Add(new FinancialHealthInsight(
                    everydayChange > 0 ? "Everyday spending increased" : "Everyday spending improved",
                    $"Everyday spending is {Math.Abs(everydayChange):C} {(everydayChange > 0 ? "higher" : "lower")} than last month.",
                    everydayChange > 0 ? "warning" : "positive",
                    everydayChange > 0 ? "arrow-up-right" : "arrow-down-right"));
            }
        }

        if (committedPercent >= 100m && remaining >= 0)
        {
            insights.Add(new FinancialHealthInsight(
                "All income allocated",
                "Every pound of available income is currently assigned. Review the month before adding further commitments.",
                "warning",
                "gauge"));
        }

        return insights.Take(4).ToList();
    }
}
