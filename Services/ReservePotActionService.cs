using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public interface IReservePotActionService
{
    Task<ReservePotActionResult> PayInFullAsync(int potId, string operationKey);
    Task<ReservePotWithdrawalViewModel?> BuildWithdrawalAsync(int potId);
    Task<ReservePotActionResult> WithdrawAsync(ReservePotWithdrawalViewModel input);
    Task<ReservePotActionResult> RestoreNegativeBalanceAsync(int potId, decimal amount, string operationKey);
}

public sealed class ReservePotActionService(
    FinanceRepository repo,
    IReserveAccountSelectionService reserveAccountSelectionService) : IReservePotActionService
{
    public async Task<ReservePotActionResult> PayInFullAsync(int potId, string operationKey)
    {
        var pots = await repo.GetReservePotsAsync();
        var pot = pots.FirstOrDefault(x => x.Id == potId);
        if (pot is null || !pot.IsActive)
            return Failure(potId, pot?.Name, "The selected pot is not active or could not be found.");
        if (!pot.TargetAmount.HasValue)
            return Failure(potId, pot.Name, "Set a target before paying this pot in full.");

        var required = Math.Max(0m, pot.TargetAmount.Value - pot.AllocatedAmount);
        if (required <= 0m)
            return Failure(potId, pot.Name, "This pot is already fully funded.");

        var accountSummary = await reserveAccountSelectionService.BuildSummaryAsync();
        var allocated = pots.Where(x => x.IsActive).Sum(x => Math.Max(0m, x.AllocatedAmount));
        var available = Math.Max(0m, accountSummary.SurplusAboveBaseline - allocated);
        if (available < required)
            return Failure(potId, pot.Name, $"There is not enough unallocated reserve money. A further {(required - available):C} is required.");

        return await repo.PayReservePotInFullAsync(potId, required, operationKey);
    }

    public async Task<ReservePotWithdrawalViewModel?> BuildWithdrawalAsync(int potId)
    {
        var pot = (await repo.GetReservePotsAsync()).FirstOrDefault(x => x.Id == potId);
        return pot is null ? null : new ReservePotWithdrawalViewModel
        {
            PotId = pot.Id,
            PotName = pot.Name,
            CurrentBalance = pot.AllocatedAmount
        };
    }

    public async Task<ReservePotActionResult> WithdrawAsync(ReservePotWithdrawalViewModel input)
    {
        if (input.Amount <= 0m)
            return Failure(input.PotId, input.PotName, "The withdrawal amount must be greater than zero.");
        if (input.WithdrawalDate.Date > DateTime.Today)
            return Failure(input.PotId, input.PotName, "The withdrawal date cannot be in the future.");
        if (string.IsNullOrWhiteSpace(input.Reason))
            return Failure(input.PotId, input.PotName, "Enter a reason for the withdrawal.");

        return await repo.WithdrawFromReservePotAsync(
            input.PotId, input.Amount, input.WithdrawalDate, input.Reason, input.OperationKey);
    }

    public async Task<ReservePotActionResult> RestoreNegativeBalanceAsync(int potId, decimal amount, string operationKey)
    {
        if (amount <= 0m)
            return Failure(potId, null, "The recovery contribution must be greater than zero.");

        var pot = (await repo.GetReservePotsAsync()).FirstOrDefault(x => x.Id == potId);
        if (pot is null || !pot.IsActive)
            return Failure(potId, pot?.Name, "The selected pot is not active or could not be found.");
        if (!pot.IsOverdrawn)
            return Failure(potId, pot.Name, "This pot no longer has a negative balance.");

        return await repo.RestoreNegativeReservePotBalanceAsync(potId, amount, operationKey);
    }

    private static ReservePotActionResult Failure(int potId, string? potName, string message) => new()
    {
        PotId = potId,
        PotName = potName ?? string.Empty,
        Message = message
    };
}
