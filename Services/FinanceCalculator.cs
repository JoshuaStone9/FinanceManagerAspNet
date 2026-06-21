using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public sealed class FinanceCalculator
{
    public decimal CompoundMonthly(decimal principal, decimal annualRate, decimal monthlyContribution, int months)
    {
        if (months <= 0) return Math.Round(principal, 2);

        var monthlyRate = annualRate / 100m / 12m;
        var total = principal;

        for (var i = 0; i < months; i++)
        {
            // Contribution is added first because these pots are usually updated at the start of each monthly budget cycle.
            total = (total + monthlyContribution) * (1 + monthlyRate);
        }

        return Math.Round(total, 2);
    }

    public decimal ProjectSalarySavings(decimal monthlySavingTarget, int months) =>
        Math.Round(Math.Max(0, months) * monthlySavingTarget, 2);

    public InterestProjection ProjectAccount(AccountBalance account, int months)
    {
        var projected = CompoundMonthly(account.Amount, account.InterestRate, account.MonthlyContribution, months);
        var contributions = Math.Round(account.MonthlyContribution * Math.Max(0, months), 2);
        var interest = Math.Round(projected - account.Amount - contributions, 2);
        if (interest < 0) interest = 0;

        return new InterestProjection(
            account.Name,
            account.Amount,
            account.InterestRate,
            account.MonthlyContribution,
            months,
            contributions,
            interest,
            projected,
            account.UpdatedAt);
    }

    public List<InterestProjection> ProjectAccountsDetailed(IEnumerable<AccountBalance> accounts, int months) =>
        accounts.Where(a => a.IncludeInGlobalGoal)
                .Select(a => ProjectAccount(a, months))
                .ToList();

    public decimal ProjectAccounts(IEnumerable<AccountBalance> accounts, int months, decimal monthlySavingTarget = 0)
    {
        var accountProjection = ProjectAccountsDetailed(accounts, months).Sum(a => a.ProjectedBalance);
        var salarySavingsProjection = ProjectSalarySavings(monthlySavingTarget, months);

        return Math.Round(accountProjection + salarySavingsProjection, 2);
    }

    public decimal ProjectAccountsWithoutInterest(IEnumerable<AccountBalance> accounts, int months, decimal monthlySavingTarget = 0)
    {
        var accountProjection = accounts
            .Where(a => a.IncludeInGlobalGoal)
            .Sum(a => a.Amount + (a.MonthlyContribution * Math.Max(0, months)));

        var salarySavingsProjection = ProjectSalarySavings(monthlySavingTarget, months);

        return Math.Round(accountProjection + salarySavingsProjection, 2);
    }

    public int MonthsToGoal(decimal totalNow, decimal goal, decimal monthlyPace)
    {
        if (totalNow >= goal) return 0;
        if (monthlyPace <= 0) return -1;
        return (int)Math.Ceiling((goal - totalNow) / monthlyPace);
    }

    public int MonthsToGoalWithInterest(
        IEnumerable<AccountBalance> accounts,
        decimal goal,
        decimal monthlySavingTarget = 0,
        int maxMonths = 240)
    {
        var included = accounts.Where(a => a.IncludeInGlobalGoal).ToList();
        if (included.Sum(a => a.Amount) >= goal) return 0;

        for (var month = 1; month <= maxMonths; month++)
        {
            if (ProjectAccounts(included, month, monthlySavingTarget) >= goal) return month;
        }

        return -1;
    }
}
