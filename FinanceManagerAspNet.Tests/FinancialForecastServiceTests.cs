using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class FinancialForecastServiceTests
{
    private readonly FinancialForecastService _service = new();
    private static readonly DateTime Start = new(2026, 7, 1);

    [Fact]
    public void Build_ProtectsBaselineAndCalculatesUnallocatedSurplus()
    {
        var result = Build(accounts: [Account(1, 15000m, 0m, 0m)], pots: []);

        Assert.Equal(15000m, result.ProjectedReserveBalance);
        Assert.Equal(3000m, result.ProjectedSurplusAboveBaseline);
        Assert.Equal(3000m, result.ProjectedUnallocatedSurplus);
    }

    [Fact]
    public void Build_IncludesMonthlyContributionsWhenEnabled()
    {
        var result = Build(accounts: [Account(1, 12000m, 0m, 300m)], pots: [], months: 3);

        Assert.Equal(900m, result.ProjectedAccountContributions);
        Assert.Equal(12900m, result.ProjectedReserveBalance);
    }

    [Fact]
    public void Build_ExcludesMonthlyContributionsWhenDisabled()
    {
        var result = Build(accounts: [Account(1, 12000m, 0m, 300m)], pots: [], months: 3, includeContributions: false);

        Assert.Equal(0m, result.ProjectedAccountContributions);
        Assert.Equal(12000m, result.ProjectedReserveBalance);
    }

    [Fact]
    public void Build_CalculatesMonthlyInterest()
    {
        var result = Build(accounts: [Account(1, 12000m, 12m, 0m)], pots: [], months: 1);

        Assert.Equal(120m, result.ProjectedInterest);
        Assert.Equal(12120m, result.ProjectedReserveBalance);
    }

    [Fact]
    public void Build_ReturnsCompletedWhenTargetAlreadyReached()
    {
        var result = Build(pots: [Pot(balance: 1000m, target: 1000m, contribution: 0m, dueDate: Start.AddMonths(6))]);

        Assert.Equal(ForecastGoalStatus.Completed, result.Pots.Single().Status);
    }

    [Fact]
    public void Build_ReturnsOverdrawnBeforeOtherStatuses()
    {
        var result = Build(pots: [Pot(balance: -200m, target: 1000m, contribution: 100m, dueDate: Start.AddMonths(12))]);

        Assert.Equal(ForecastGoalStatus.Overdrawn, result.Pots.Single().Status);
    }

    [Fact]
    public void Build_ReturnsNoTargetWhenTargetMissing()
    {
        var result = Build(pots: [Pot(balance: 100m, target: null, contribution: 100m, dueDate: Start.AddMonths(12))]);

        Assert.Equal(ForecastGoalStatus.NoTarget, result.Pots.Single().Status);
    }

    [Fact]
    public void Build_ReturnsNoDueDateWhenDueDateMissing()
    {
        var result = Build(pots: [Pot(balance: 100m, target: 1000m, contribution: 100m, dueDate: null)]);

        Assert.Equal(ForecastGoalStatus.NoDueDate, result.Pots.Single().Status);
    }

    [Fact]
    public void Build_ReturnsNoContributionPlannedWhenContributionIsZero()
    {
        var result = Build(pots: [Pot(balance: 100m, target: 1000m, contribution: 0m, dueDate: Start.AddMonths(12))]);

        Assert.Equal(ForecastGoalStatus.NoContributionPlanned, result.Pots.Single().Status);
    }

    [Fact]
    public void Build_ReturnsOnTrackWhenCompletionIsBeforeDueDate()
    {
        var result = Build(pots: [Pot(balance: 0m, target: 1200m, contribution: 100m, dueDate: Start.AddMonths(12))], months: 12);

        var pot = result.Pots.Single();
        Assert.Equal(ForecastGoalStatus.OnTrack, pot.Status);
        Assert.Equal(100m, pot.RequiredMonthlyContribution);
    }

    [Fact]
    public void Build_ReturnsAtRiskWhenCompletionIsWithinTwoMonthsAfterDueDate()
    {
        var result = Build(pots: [Pot(balance: 0m, target: 1200m, contribution: 100m, dueDate: Start.AddMonths(10))], months: 12);

        Assert.Equal(ForecastGoalStatus.AtRisk, result.Pots.Single().Status);
    }

    [Fact]
    public void Build_ReturnsBehindWhenCompletionIsMoreThanTwoMonthsLate()
    {
        var result = Build(pots: [Pot(balance: 0m, target: 1200m, contribution: 50m, dueDate: Start.AddMonths(12))], months: 24);

        var pot = result.Pots.Single();
        Assert.Equal(ForecastGoalStatus.Behind, pot.Status);
        Assert.Equal(50m, pot.AdditionalMonthlyContributionRequired);
    }

    private FinancialForecastResult Build(
        IReadOnlyList<ReserveAccountOption>? accounts = null,
        IReadOnlyList<ReservePot>? pots = null,
        int months = 12,
        bool includeContributions = true)
        => _service.Build(new FinancialForecastRequest
        {
            StartDate = Start,
            EndDate = Start.AddMonths(months),
            ProtectedReserveBaseline = 12000m,
            IncludeAccountContributions = includeContributions,
            Accounts = accounts ?? [Account(1, 12000m, 0m, 0m)],
            Pots = pots ?? []
        });

    private static ReserveAccountOption Account(int id, decimal balance, decimal rate, decimal monthlyContribution)
        => new(id, $"Account {id}", balance, rate, monthlyContribution, true);

    private static ReservePot Pot(decimal balance, decimal? target, decimal contribution, DateTime? dueDate)
        => new(
            1,
            "Test pot",
            balance,
            contribution,
            contribution,
            "Monthly",
            null,
            true,
            true,
            null,
            null,
            null,
            Start,
            target,
            dueDate,
            1,
            true,
            null,
            Start);
}
