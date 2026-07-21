using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceManagerAspNet.Services;

public sealed class FinanceActionAuditFilter(
    FinanceRepository repo,
    ILogger<FinanceActionAuditFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var executed = await next();

        if (!HttpMethods.IsPost(context.HttpContext.Request.Method)
            || context.HttpContext.User.Identity?.IsAuthenticated != true
            || executed.Exception is not null)
        {
            return;
        }

        var controller = context.Controller
            .GetType()
            .Name
            .Replace("Controller", string.Empty);

        var action = context.ActionDescriptor.RouteValues.TryGetValue(
            "action",
            out var value)
                ? value ?? "Action"
                : "Action";

        if (controller == "Auth")
        {
            return;
        }

        try
        {
            await repo.AddFinanceEventAsync(
                NormaliseArea(controller),
                "AuditAction",
                controller,
                TryGetEntityId(context.ActionArguments),
                $"{SplitWords(action)} completed",
                $"A change was made in {SplitWords(controller)}.",
                TryGetAmount(context.ActionArguments),
                "Application");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Unable to write audit event for {Controller}.{Action}",
                controller,
                action);
        }
    }

    private static string NormaliseArea(string controller) => controller switch
    {
        "Reconciliation" or "Settings" => "Accounts & Settings",
        "SavingPots" or "MoneyPotActivity" => "Household Reserve",
        "MonthlyMoney" or "Dashboard" => "Monthly Money",
        "Assets" => "Assets",
        "Items" or "Locations" or "VaultSettings" => "Personal Vault",
        _ => SplitWords(controller)
    };

    private static int? TryGetEntityId(
        IDictionary<string, object?> arguments)
    {
        foreach (var key in new[]
        {
            "id",
            "potId",
            "accountId",
            "itemId"
        })
        {
            if (arguments.TryGetValue(key, out var value)
                && value is not null
                && int.TryParse(value.ToString(), out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static decimal? TryGetAmount(
        IDictionary<string, object?> arguments)
    {
        foreach (var key in new[]
        {
            "amount",
            "actualBalance",
            "openingBalance",
            "allocatedAmount"
        })
        {
            if (arguments.TryGetValue(key, out var value)
                && value is not null
                && decimal.TryParse(value.ToString(), out var amount))
            {
                return amount;
            }
        }

        return null;
    }

    private static string SplitWords(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value,
            "([a-z])([A-Z])",
            "$1 $2");
}