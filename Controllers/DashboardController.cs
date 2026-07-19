using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class DashboardController(
    FinanceRepository repo,
    IDashboardSummaryService dashboardSummaryService,
    IDashboardExperienceService dashboardExperienceService,
    IMonthlyFinancialHealthService monthlyFinancialHealthService,
    IReserveAccountSelectionService reserveAccountSelectionService,
    IFinancialForecastService financialForecastService,
    IWhatIfForecastService whatIfForecastService,
    IForecastScenarioService forecastScenarioService) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month, bool manage = false, string? source = null)
    {
        var now = DateTime.Today;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        await repo.EnsureModernTablesAsync();

        var incomeEntriesTotal = await repo.GetMonthlyIncomeEntriesTotalAsync(y, m);
        var monthlyIncome = incomeEntriesTotal ?? 0m;
        var passiveRecords = await repo.GetPassiveIncomeRecordsAsync(y, m);
        var passiveIncome = passiveRecords.Sum(x => x.ActualAmount);
        var operatingIncome = Math.Max(0m, monthlyIncome - passiveIncome);
        var carryForwardInfo = await repo.GetCarryForwardInfoAsync(y, m);
        var carryForward = carryForwardInfo.EffectiveAmount;

        var bills = await repo.GetRowsAsync("bills", m, y);
        var everyday = await repo.GetRowsAsync("everyday_spending", m, y);
        var extras = await repo.GetRowsAsync("extra_expenses", m, y);
        var investments = await repo.GetRowsAsync("investments", m, y);
        var reserveAllocations = await repo.GetRowsAsync("savings", m, y);
        var reserve = await repo.GetHouseholdReserveAsync();
        var reservePots = await repo.GetReservePotsAsync();

        var vm = new DashboardViewModel
        {
            Year = y, Month = m, MonthlyIncome = monthlyIncome, OperatingIncome = operatingIncome, PassiveIncome = passiveIncome, CarryForwardAmount = carryForward,
            CarryForwardCalculated = carryForwardInfo.CalculatedAmount, CarryForwardOverride = carryForwardInfo.OverrideAmount,
            CarryForwardOverrideReason = carryForwardInfo.OverrideReason, SickDays = 0,
            Bills = bills, Expenses = everyday, ExtraExpenses = extras, Investments = investments, Savings = reserveAllocations,
            BillsTotal = bills.Sum(x => x.Amount), ExpensesTotal = everyday.Sum(x => x.Amount), ExtraExpensesTotal = extras.Sum(x => x.Amount),
            InvestmentsTotal = investments.Sum(x => x.Amount), SavingsTotal = reserveAllocations.Sum(x => x.Amount),
            TotalGoalBalance = reserve.Balance, Accounts = [],
            LastModified = [new LastModifiedInfo("Household reserve", reserve.UpdatedAt)]
        };

        ViewBag.ReserveProvider = reserve.Provider;
        ViewBag.ReserveInterestRate = reserve.InterestRate;
        var reserveAllocated = reservePots.Where(p => p.IsActive).Sum(p => p.AllocatedAmount);
        ViewBag.ReserveAllocated = reserveAllocated;
        ViewBag.ReserveUnallocated = reserve.Balance - reserveAllocated;
        vm.Intelligence = await dashboardSummaryService.BuildAsync(reserve, reservePots);

        var accountSummary = await reserveAccountSelectionService.BuildSummaryAsync();
        var preferredScenario = await forecastScenarioService.GetPreferredAsync();
        FinancialForecastResult twelveMonthForecast;
        if (preferredScenario is not null)
        {
            var preferredInput = preferredScenario.ToInput();
            preferredInput.Months = 12;
            twelveMonthForecast = whatIfForecastService.Build(preferredInput, accountSummary, reservePots).ScenarioPlan;
        }
        else
        {
            var investmentStages = await repo.GetReservePotInvestmentStagesAsync();
            twelveMonthForecast = financialForecastService.Build(new FinancialForecastRequest
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(12),
                ProtectedReserveBaseline = accountSummary.Baseline,
                Accounts = accountSummary.SelectedAccounts,
                Pots = reservePots,
                InvestmentStages = investmentStages
            });
        }

        vm.ForecastSummary = new DashboardForecastSummary
        {
            ProjectedReserveBalance = twelveMonthForecast.ProjectedReserveBalance,
            ProjectedInterest = twelveMonthForecast.ProjectedInterest,
            GoalsAtRisk = twelveMonthForecast.AtRiskPotCount,
            GoalsExpectedToComplete = twelveMonthForecast.CompletedPotCount,
            ScenarioLabel = preferredScenario?.Name ?? "Live plan",
            PreferredScenarioId = preferredScenario?.Id
        };

        vm.Experience = dashboardExperienceService.Build(vm);

        var smartDashboardTasks = vm.Experience.UpcomingActions.ToList();
        var dashboardAccounts = await repo.GetAccountsAsync(reserve.Balance);
        smartDashboardTasks.AddRange(dashboardAccounts
            .Where(x => x.Amount > 0m && x.UpdatedAt.Date <= DateTime.Today.AddDays(-90))
            .Select(x => new DashboardActionItem(
                $"Check {x.Name} balance",
                $"Last updated {x.UpdatedAt:dd MMM yyyy}",
                "~/Reconciliation",
                x.UpdatedAt.Date <= DateTime.Today.AddDays(-180) ? "danger" : "warning",
                DateTime.Today)));

        var reconciliation = await repo.GetAccountReconciliationAsync();
        if (reconciliation.AccountsNeedingInterestReconciliation > 0)
        {
            smartDashboardTasks.Add(new DashboardActionItem(
                "Apply pending account interest",
                $"{reconciliation.TotalPendingInterest:C} across {reconciliation.AccountsNeedingInterestReconciliation} account{(reconciliation.AccountsNeedingInterestReconciliation == 1 ? string.Empty : "s")}",
                "~/Reconciliation",
                "warning",
                DateTime.Today));
        }

        var expectedPassiveIncome = await repo.GetPassiveIncomeEstimatesAsync(y, m);
        smartDashboardTasks.AddRange(expectedPassiveIncome
            .Where(x => !x.IsReceived && x.EstimatedAmount > 0m)
            .Select(x => new DashboardActionItem(
                $"Confirm {x.SourceName} interest",
                $"Approximately {x.EstimatedAmount:C} expected this month",
                $"~/MonthlyMoney/Income?year={y}&month={m}",
                "warning",
                new DateTime(y, m, DateTime.DaysInMonth(y, m)))));

        vm.Experience.UpcomingActions = smartDashboardTasks
            .GroupBy(x => new { x.Title, x.Url })
            .Select(x => x.First())
            .OrderBy(x => x.Severity == "danger" ? 0 : x.Severity == "warning" ? 1 : 2)
            .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
            .Take(6)
            .ToList();

        var previousMonth = new DateTime(y, m, 1).AddMonths(-1);
        var previousIncomeEntriesTotal = await repo.GetMonthlyIncomeEntriesTotalAsync(previousMonth.Year, previousMonth.Month);
        var previousIncome = previousIncomeEntriesTotal ?? 0m;
        var previousCarryForward = (await repo.GetCarryForwardInfoAsync(previousMonth.Year, previousMonth.Month)).EffectiveAmount;
        var previousBills = await repo.GetRowsAsync("bills", previousMonth.Month, previousMonth.Year);
        var previousEveryday = await repo.GetRowsAsync("everyday_spending", previousMonth.Month, previousMonth.Year);
        var previousExtras = await repo.GetRowsAsync("extra_expenses", previousMonth.Month, previousMonth.Year);
        var previousInvestments = await repo.GetRowsAsync("investments", previousMonth.Month, previousMonth.Year);
        var previousMoneyPots = await repo.GetRowsAsync("savings", previousMonth.Month, previousMonth.Year);

        var hasPreviousMonthData = previousIncomeEntriesTotal.HasValue
            || previousBills.Count > 0
            || previousEveryday.Count > 0
            || previousExtras.Count > 0
            || previousInvestments.Count > 0
            || previousMoneyPots.Count > 0;

        vm.FinancialHealth = monthlyFinancialHealthService.Build(
            new MonthlyFinancialSnapshot(
                monthlyIncome,
                carryForward,
                vm.BillsTotal,
                vm.ExpensesTotal,
                vm.ExtraExpensesTotal,
                vm.InvestmentsTotal,
                vm.SavingsTotal),
            hasPreviousMonthData
                ? new MonthlyFinancialSnapshot(
                    previousIncome,
                    previousCarryForward,
                    previousBills.Sum(x => x.Amount),
                    previousEveryday.Sum(x => x.Amount),
                    previousExtras.Sum(x => x.Amount),
                    previousInvestments.Sum(x => x.Amount),
                    previousMoneyPots.Sum(x => x.Amount))
                : null);

        if (manage)
        {
            ViewBag.SelectedSource = source;
            ViewBag.ExistingBills = await repo.GetExistingPaymentOptionsAsync("bills");
            ViewBag.ExistingEveryday = await repo.GetExistingPaymentOptionsAsync("everyday_spending");
            ViewBag.ExistingExtras = await repo.GetExistingPaymentOptionsAsync("extra_expenses");
            ViewBag.ExistingInvestments = await repo.GetExistingPaymentOptionsAsync("investments");
            ViewBag.ExistingSavings = await repo.GetExistingPaymentOptionsAsync("savings");
        }

        return View(manage ? "ManageMonth" : "Index", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIncome(int year, int month, decimal amount, int sickDays)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveIncomeAsync(year, month, Math.Max(0, amount), Math.Max(0, sickDays));
        return RedirectToDashboard(year, month, "income");
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCarryForwardOverride(int year, int month, decimal amount, string? reason)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.SaveCarryForwardOverrideAsync(year, month, amount, reason);
        TempData["Success"] = "Carry forward override saved.";
        return RedirectToDashboard(year, month, "carry-forward");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateCarryForward(int year, int month)
    {
        if (!CanEdit()) return LoginRedirect();
        await repo.ClearCarryForwardOverrideAsync(year, month);
        TempData["Success"] = "Manual override removed. The calculated carry forward is now being used.";
        return RedirectToDashboard(year, month, "carry-forward");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(int year, int month, string source, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes, string? returnAnchor, bool isPermanent = false)
    {
        if (!CanEdit()) return LoginRedirect();

        name = name?.Trim() ?? string.Empty;
        var anchor = returnAnchor ?? source;

        if (month is < 1 or > 12 || string.IsNullOrWhiteSpace(name) || amount < 0)
        {
            TempData["WorkspaceError"] = string.IsNullOrWhiteSpace(name)
                ? "Enter a name before saving."
                : amount < 0
                    ? "Amount cannot be negative."
                    : "The selected month is invalid.";
            TempData["WorkspaceDraftName"] = name;
            TempData["WorkspaceDraftAmount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            TempData["WorkspaceDraftDate"] = date == default ? string.Empty : date.ToString("yyyy-MM-dd");
            TempData["WorkspaceDraftCategory"] = category;
            TempData["WorkspaceDraftType"] = type;
            TempData["WorkspaceDraftLength"] = length;
            TempData["WorkspaceDraftNotes"] = notes;
            return RedirectToWorkspace(year, month, anchor, focusQuickEntry: true);
        }

        var selectedDate = date == default
            ? new DateTime(year, month, Math.Min(DateTime.Today.Day, DateTime.DaysInMonth(year, month)))
            : date;

        await repo.AddPaymentAsync(source, name, amount, selectedDate, category, type, length, notes);
        if (source == "savings" && amount > 0)
            await repo.ApplyReserveAllocationAsync(name, amount);

        if (isPermanent && source != "extra_expenses")
            await repo.UpsertMonthlyEntryTemplateAsync(source, name, amount, category, type, length, notes);

        TempData["Success"] = source == "savings"
            ? $"Contribution added to {name}."
            : $"{name} added.";

        return RedirectToWorkspace(year, month, anchor, focusQuickEntry: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePayment(int year, int month, string source, int id, string? returnAnchor)
    {
        if (!CanEdit()) return LoginRedirect();
        var existing = await repo.GetPaymentAsync(source, id);
        await repo.DeletePaymentAsync(source, id);
        if (source == "savings" && existing is not null) await repo.ApplyReserveAllocationAsync(existing.Name, -existing.Amount);
        return RedirectToDashboard(year, month, returnAnchor ?? source);
    }

    [HttpGet]
    public async Task<IActionResult> EditPayment(string source, int id, int year, int month)
    {
        if (!CanEdit()) return LoginRedirect();
        var item = await repo.GetPaymentAsync(source, id);
        if (item is null) return NotFound();
        ViewBag.Year = year;
        ViewBag.Month = month;
        ViewBag.ReturnAction = source switch
        {
            "income" => "Income",
            "bills" => "EssentialBills",
            "everyday_spending" => "EverydaySpending",
            "extra_expenses" => "ExtraExpenses",
            "investments" => "Investments",
            "savings" => "MoneyPots",
            _ => null
        };
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPayment(int year, int month, string source, int id, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        if (!CanEdit()) return LoginRedirect();
        var existing = await repo.GetPaymentAsync(source, id);
        await repo.UpdatePaymentAsync(source, id, name, Math.Max(0, amount), date, category, type, length, notes);
        if (source == "savings" && existing is not null)
        {
            await repo.ApplyReserveAllocationAsync(existing.Name, -existing.Amount);
            await repo.ApplyReserveAllocationAsync(name, Math.Max(0, amount));
        }
        return RedirectToDashboard(year, month, source);
    }

    [HttpGet]
    public IActionResult CarryOver(int year, int month)
        => RedirectToAction(nameof(PrepareNextMonth), new { year, month });

    [HttpGet]
    public async Task<IActionResult> PrepareNextMonth(int year, int month)
    {
        if (!CanEdit()) return LoginRedirect();
        if (month is < 1 or > 12) return BadRequest("Month must be between 1 and 12.");

        var next = new DateTime(year, month, 1).AddMonths(1);
        var definitions = new[]
        {
            (Source: "bills", Title: "Essential bills"),
            (Source: "everyday_spending", Title: "Everyday spending"),
            (Source: "investments", Title: "Investments"),
            (Source: "savings", Title: "Money pots")
        };

        var sections = new List<PrepareNextMonthSection>();
        foreach (var definition in definitions)
        {
            var items = await repo.GetMissingMonthlyEntryTemplatesAsync(
                definition.Source,
                next.Year,
                next.Month);

            if (items.Count == 0) continue;

            sections.Add(new PrepareNextMonthSection
            {
                Source = definition.Source,
                Title = definition.Title,
                Items = items
            });
        }

        return View(new PrepareNextMonthViewModel
        {
            Year = year,
            Month = month,
            Sections = sections,
            MonthResult = await repo.GetMonthResultAsync(year, month)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPrepareNextMonth(int year, int month)
    {
        if (!CanEdit()) return LoginRedirect();
        if (month is < 1 or > 12) return BadRequest("Month must be between 1 and 12.");

        var next = new DateTime(year, month, 1).AddMonths(1);
        var sources = new[] { "bills", "everyday_spending", "investments", "savings" };
        var added = 0;

        foreach (var source in sources)
        {
            var items = await repo.GetMissingMonthlyEntryTemplatesAsync(source, next.Year, next.Month);
            added += await repo.SetupMonthFromTemplatesAsync(
                next.Year,
                next.Month,
                source,
                items.Select(x => new MonthSetupItemInput
                {
                    TemplateId = x.Id,
                    Include = true,
                    Amount = x.DefaultAmount
                }));
        }

        var monthResult = await repo.CarryMonthResultForwardAsync(year, month);
        var resultText = monthResult switch
        {
            > 0 => $" An excess of {monthResult:C} was carried forward.",
            < 0 => $" A shortfall of {Math.Abs(monthResult):C} was carried forward.",
            _ => " No excess or shortfall needed carrying forward."
        };

        TempData["Success"] = added == 0
            ? $"{next:MMMM yyyy} was already prepared. Existing entries were left unchanged." + resultText
            : $"{next:MMMM yyyy} prepared with {added} recurring entr{(added == 1 ? "y" : "ies")}." + resultText;

        return RedirectToAction(nameof(Index), new { year = next.Year, month = next.Month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AllocateRemainingToReserve(int year, int month, decimal amount)
    {
        if (!CanEdit()) return LoginRedirect();
        if (amount > 0)
        {
            await repo.AddPaymentAsync("savings", "Unallocated reserve", amount, new DateTime(year, month, 1), null, null, null, "Allocated from the remaining monthly surplus");
            await repo.ApplyReserveAllocationAsync("Unallocated reserve", amount);
            TempData["Success"] = $"£{amount:N2} allocated to the household reserve.";
        }
        return RedirectToDashboard(year, month, "savings");
    }

    private IActionResult RedirectToDashboard(int year, int month, string anchor)
        => RedirectToWorkspace(year, month, anchor, focusQuickEntry: false);

    private IActionResult RedirectToWorkspace(int year, int month, string anchor, bool focusQuickEntry)
    {
        var workspaceAction = anchor switch
        {
            "bills" => "EssentialBills",
            "everyday_spending" or "everyday" => "EverydaySpending",
            "extra_expenses" or "extras" => "ExtraExpenses",
            "investments" => "Investments",
            "savings" => "MoneyPots",
            _ => null
        };

        if (workspaceAction is not null)
            return RedirectToAction(workspaceAction, "MonthlyMoney", new
            {
                year,
                month,
                focus = focusQuickEntry ? "quick-entry" : null
            });

        return Redirect($"{Url.Action(nameof(Index), new { year, month, manage = true, source = anchor })}#{anchor}");
    }
    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
