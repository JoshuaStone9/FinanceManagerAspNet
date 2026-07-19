using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IReserveAccountSelectionService
{
    Task<HouseholdReserveAccountSummary> BuildSummaryAsync(decimal? baseline = null);
    Task SaveSelectionAsync(IEnumerable<int> accountIds);
}

public sealed class ReserveAccountSelectionService(FinanceRepository repo) : IReserveAccountSelectionService
{
    public async Task<HouseholdReserveAccountSummary> BuildSummaryAsync(decimal? baseline = null)
    {
        var configuredBaseline = baseline ?? await repo.GetDecimalSettingAsync("EmergencyFundBaseline", 12000m);
        var emergencyFund = await repo.GetEmergencyFundAsync();
        var accounts = await repo.GetAccountsAsync(emergencyFund);
        var selectedIds = await repo.GetSelectedReserveAccountIdsAsync();

        var available = accounts
            .OrderBy(x => x.Id == 0 ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => new ReserveAccountOption(
                x.Id, x.Name, x.Amount, x.InterestRate, x.MonthlyContribution, selectedIds.Contains(x.Id), x.TaxTreatment, x.TaxRate, x.TaxEffectiveFrom))
            .ToList();

        return new HouseholdReserveAccountSummary
        {
            Baseline = Math.Max(0m, configuredBaseline),
            AvailableAccounts = available,
            SelectedAccounts = available.Where(x => x.IsSelected).ToList()
        };
    }

    public Task SaveSelectionAsync(IEnumerable<int> accountIds)
        => repo.SaveReserveAccountSelectionAsync(accountIds);
}
