using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IReserveAccountSelectionService
{
    Task<HouseholdReserveAccountSummary> BuildSummaryAsync(decimal baseline = 12000m);
    Task SaveSelectionAsync(IEnumerable<int> accountIds);
}

public sealed class ReserveAccountSelectionService(FinanceRepository repo) : IReserveAccountSelectionService
{
    public async Task<HouseholdReserveAccountSummary> BuildSummaryAsync(decimal baseline = 12000m)
    {
        var emergencyFund = await repo.GetEmergencyFundAsync();
        var accounts = await repo.GetAccountsAsync(emergencyFund);
        var selectedIds = await repo.GetSelectedReserveAccountIdsAsync();

        var available = accounts
            .OrderBy(x => x.Id == 0 ? 0 : 1)
            .ThenBy(x => x.Name)
            .Select(x => new ReserveAccountOption(
                x.Id, x.Name, x.Amount, x.InterestRate, selectedIds.Contains(x.Id)))
            .ToList();

        return new HouseholdReserveAccountSummary
        {
            Baseline = Math.Max(0m, baseline),
            AvailableAccounts = available,
            SelectedAccounts = available.Where(x => x.IsSelected).ToList()
        };
    }

    public Task SaveSelectionAsync(IEnumerable<int> accountIds)
        => repo.SaveReserveAccountSelectionAsync(accountIds);
}
