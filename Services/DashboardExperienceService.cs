using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IDashboardExperienceService
{
    DashboardExperience Build(DashboardViewModel dashboard);
}

public sealed class DashboardExperienceService : IDashboardExperienceService
{
    public DashboardExperience Build(DashboardViewModel dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);

        var availableBeforeFuture = dashboard.EffectiveIncome
            - dashboard.BillsTotal
            - dashboard.ExpensesTotal
            - dashboard.ExtraExpensesTotal;

        var activity = dashboard.Bills
            .Concat(dashboard.Expenses)
            .Concat(dashboard.ExtraExpenses)
            .Concat(dashboard.Investments)
            .Concat(dashboard.Savings)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Take(8)
            .Select(ToActivity)
            .ToList();

        var actions = new List<DashboardActionItem>();
        actions.AddRange(dashboard.Intelligence.DueReminders.Take(4).Select(x =>
            new DashboardActionItem(
                x.Title,
                $"{x.PotName ?? "General"} · due {x.EffectiveDueDate:dd MMM yyyy}",
                "~/Reminders",
                x.EffectiveDueDate.Date < DateTime.Today ? "danger" : "warning",
                x.EffectiveDueDate)));

        actions.AddRange(dashboard.Intelligence.UpcomingTargets.Take(4).Select(x =>
            new DashboardActionItem(
                x.PotName,
                $"{x.RemainingAmount:C} remaining · target {x.DueDate:dd MMM yyyy}",
                $"~/SavingPots#pot-{x.PotId}",
                x.DaysRemaining <= 30 ? "warning" : "neutral",
                x.DueDate)));

        if (dashboard.Intelligence.OverdueOrMissedCount > 0)
        {
            actions.Insert(0, new DashboardActionItem(
                $"{dashboard.Intelligence.OverdueOrMissedCount} pot{(dashboard.Intelligence.OverdueOrMissedCount == 1 ? "" : "s")} need attention",
                "Review overdue or missed funding",
                "~/SavingPots",
                "danger"));
        }

        return new DashboardExperience
        {
            Journey = new MonthlyMoneyJourney
            {
                Income = dashboard.MonthlyIncome,
                CarryForward = dashboard.CarryForwardAmount,
                EssentialBills = dashboard.BillsTotal,
                EverydaySpending = dashboard.ExpensesTotal,
                ExtraExpenses = dashboard.ExtraExpensesTotal,
                AvailableBeforeFutureAllocations = availableBeforeFuture,
                Investments = dashboard.InvestmentsTotal,
                HouseholdReserveAllocations = dashboard.SavingsTotal,
                Remaining = dashboard.RemainingFund
            },
            AllocatedToFuture = dashboard.InvestmentsTotal + dashboard.SavingsTotal,
            RecordedItemCount = dashboard.Bills.Count + dashboard.Expenses.Count + dashboard.ExtraExpenses.Count + dashboard.Investments.Count + dashboard.Savings.Count,
            MonthlySections =
            [
                new("bills", "Essential bills", dashboard.BillsTotal, dashboard.Bills.Count, "Mortgage, utilities, insurance and fixed commitments.", "bills", "receipt"),
                new("everyday", "Everyday spending", dashboard.ExpensesTotal, dashboard.Expenses.Count, "Food, fuel and normal monthly allowances.", "everyday", "shopping-basket"),
                new("extras", "Extra expenses", dashboard.ExtraExpensesTotal, dashboard.ExtraExpenses.Count, "One-off costs kept separate from recurring spending.", "extras", "circle-plus"),
                new("reserve", "Household reserve", dashboard.SavingsTotal, dashboard.Savings.Count, "Money allocated to reserve pots during this month.", "savings", "landmark", dashboard.TotalGoalBalance, "Current reserve balance")
            ],
            UpcomingActions = actions
                .OrderBy(x => x.DueDate ?? DateTime.MaxValue)
                .Take(6)
                .ToList(),
            RecentActivity = activity
        };
    }

    private static DashboardActivityItem ToActivity(PaymentRow row)
    {
        var (label, icon) = row.Source switch
        {
            "bills" => ("Bill recorded", "receipt"),
            "everyday_spending" => ("Everyday spending recorded", "shopping-basket"),
            "extra_expenses" => ("Extra expense recorded", "circle-plus"),
            "investments" => ("Investment allocated", "chart-no-axes-combined"),
            "savings" => ("Reserve allocation recorded", "landmark"),
            _ => ("Monthly item recorded", "circle-dollar-sign")
        };

        return new DashboardActivityItem(row.DisplayName, label, row.Date, icon, row.Amount);
    }
}
