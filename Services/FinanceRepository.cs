using FinanceManagerAspNet.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FinanceManagerAspNet.Services;

public sealed record CarryForwardInfo(decimal CalculatedAmount, decimal? OverrideAmount, string? OverrideReason)
{
    public decimal EffectiveAmount => OverrideAmount ?? CalculatedAmount;
}

public sealed class FinanceRepository(IConfiguration config, IFundingEngineService fundingEngine)
{
    private string ConnStr => Environment.GetEnvironmentVariable("FM_CONNECTION_STRING")
        ?? config.GetConnectionString("FinanceManager")
        ?? throw new InvalidOperationException("Missing FinanceManager connection string.");

    public async Task EnsureModernTablesAsync()
    {
        const string sql = @"
IF OBJECT_ID('dbo.bills','U') IS NULL
CREATE TABLE dbo.bills(billid int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(150) NOT NULL, amount decimal(18,2) NOT NULL, [date] date NOT NULL, [type] nvarchar(80) NULL, [length] nvarchar(50) NULL, [description] nvarchar(500) NULL);

IF OBJECT_ID('dbo.extra_expenses','U') IS NULL
CREATE TABLE dbo.extra_expenses(extra_expense_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(150) NOT NULL, amount decimal(18,2) NOT NULL, duedate date NOT NULL, category nvarchar(100) NULL, [type] nvarchar(80) NULL, [length] nvarchar(50) NULL, [description] nvarchar(500) NULL);

IF OBJECT_ID('dbo.everyday_spending','U') IS NULL
CREATE TABLE dbo.everyday_spending(everyday_spending_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(150) NOT NULL, amount decimal(18,2) NOT NULL, [date] date NOT NULL, category nvarchar(100) NULL, [type] nvarchar(80) NULL, [length] nvarchar(50) NULL, [description] nvarchar(500) NULL);

IF OBJECT_ID('dbo.investments','U') IS NULL
CREATE TABLE dbo.investments(investments_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(150) NOT NULL, amount decimal(18,2) NOT NULL, [date] date NOT NULL, category nvarchar(100) NULL, [length] nvarchar(50) NULL, notes nvarchar(500) NULL);

IF OBJECT_ID('dbo.savings','U') IS NULL
CREATE TABLE dbo.savings(savings_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(150) NOT NULL, amount decimal(18,2) NOT NULL, [date] date NOT NULL, [length] nvarchar(50) NULL, notes nvarchar(500) NULL);

IF OBJECT_ID('dbo.emergency_fund','U') IS NULL
CREATE TABLE dbo.emergency_fund(emergency_fund_id int IDENTITY(1,1) PRIMARY KEY, amount decimal(18,2) NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.emergency_fund)
INSERT INTO dbo.emergency_fund(amount) VALUES(0);

IF OBJECT_ID('dbo.monthly_allowance','U') IS NULL
CREATE TABLE dbo.monthly_allowance(month_id int NOT NULL PRIMARY KEY, amount decimal(18,2) NOT NULL DEFAULT 0);

IF OBJECT_ID('dbo.finance_settings','U') IS NULL
CREATE TABLE dbo.finance_settings([key] nvarchar(120) NOT NULL PRIMARY KEY, [value] nvarchar(300) NOT NULL, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID('dbo.app_login','U') IS NULL
CREATE TABLE dbo.app_login(app_login_id int NOT NULL CONSTRAINT PK_app_login PRIMARY KEY DEFAULT 1, password_hash nvarchar(500) NOT NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT CK_app_login_single_row CHECK (app_login_id = 1));

IF OBJECT_ID('dbo.account_balances','U') IS NULL
CREATE TABLE dbo.account_balances(account_balance_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(120) NOT NULL, amount decimal(18,2) NOT NULL, interest_rate decimal(9,4) NOT NULL, monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, include_in_global_goal bit NOT NULL DEFAULT 1, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF COL_LENGTH('dbo.account_balances','include_in_savings_command') IS NULL ALTER TABLE dbo.account_balances ADD include_in_savings_command bit NOT NULL CONSTRAINT DF_account_balances_include_in_savings_command DEFAULT 0;

IF OBJECT_ID('dbo.reserved_funds','U') IS NULL
CREATE TABLE dbo.reserved_funds(
    reserved_fund_id int IDENTITY(1,1) PRIMARY KEY,
    [name] nvarchar(150) NOT NULL,
    amount decimal(18,2) NOT NULL DEFAULT 0,
    category nvarchar(80) NOT NULL DEFAULT 'Other',
    access_speed nvarchar(80) NOT NULL DEFAULT 'Within 1 week',
    include_in_net_worth bit NOT NULL DEFAULT 1,
    deduct_from_savings_allocation bit NOT NULL DEFAULT 1,
    notes nvarchar(500) NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID('dbo.reserved_funds','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.reserved_funds WHERE [name] = 'Moneybox Deposit')
INSERT INTO dbo.reserved_funds([name], amount, category, access_speed, include_in_net_worth, deduct_from_savings_allocation, notes)
VALUES('Moneybox Deposit', 8000, 'House Deposit', 'Moneybox / LISA', 1, 1, 'Reserved for Moneybox house deposit, excluded from savings allocation but still part of net worth.');

IF OBJECT_ID('dbo.reserve_account_selections','U') IS NULL
CREATE TABLE dbo.reserve_account_selections(
    account_id int NOT NULL PRIMARY KEY,
    display_order int NOT NULL DEFAULT 0,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID('dbo.account_balance_history','U') IS NULL
CREATE TABLE dbo.account_balance_history(history_id int IDENTITY(1,1) PRIMARY KEY, account_balance_id int NULL, [name] nvarchar(120) NOT NULL, amount decimal(18,2) NOT NULL, interest_rate decimal(9,4) NOT NULL, monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID('dbo.monthly_income_stats','U') IS NULL
CREATE TABLE dbo.monthly_income_stats(income_id int IDENTITY(1,1) PRIMARY KEY, [year] int NOT NULL, [month] int NOT NULL, amount decimal(18,2) NOT NULL, sick_days int NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT UQ_monthly_income_stats UNIQUE([year],[month]));

IF OBJECT_ID('dbo.monthly_carry_forward','U') IS NULL
CREATE TABLE dbo.monthly_carry_forward(
    monthly_carry_forward_id int IDENTITY(1,1) PRIMARY KEY,
    [year] int NOT NULL,
    [month] int NOT NULL,
    amount decimal(18,2) NOT NULL DEFAULT 0,
    override_amount decimal(18,2) NULL,
    override_reason nvarchar(500) NULL,
    source_year int NULL,
    source_month int NULL,
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_monthly_carry_forward UNIQUE([year],[month])
);

IF OBJECT_ID('dbo.monthly_carry_forward','U') IS NOT NULL AND COL_LENGTH('dbo.monthly_carry_forward','override_amount') IS NULL
    ALTER TABLE dbo.monthly_carry_forward ADD override_amount decimal(18,2) NULL;
IF OBJECT_ID('dbo.monthly_carry_forward','U') IS NOT NULL AND COL_LENGTH('dbo.monthly_carry_forward','override_reason') IS NULL
    ALTER TABLE dbo.monthly_carry_forward ADD override_reason nvarchar(500) NULL;

IF OBJECT_ID('dbo.household_reserve','U') IS NULL
CREATE TABLE dbo.household_reserve(household_reserve_id int NOT NULL CONSTRAINT PK_household_reserve PRIMARY KEY DEFAULT 1, balance decimal(18,2) NOT NULL DEFAULT 0, interest_rate decimal(9,4) NOT NULL DEFAULT 0, provider nvarchar(160) NOT NULL DEFAULT 'Money market fund', updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT CK_household_reserve_single_row CHECK (household_reserve_id = 1));

IF NOT EXISTS (SELECT 1 FROM dbo.household_reserve WHERE household_reserve_id = 1)
INSERT INTO dbo.household_reserve(household_reserve_id,balance,interest_rate,provider)
SELECT 1, ISNULL((SELECT TOP 1 amount FROM dbo.emergency_fund ORDER BY updated_at DESC),0), 0, 'Money market fund';

IF OBJECT_ID('dbo.reserve_pots','U') IS NULL
CREATE TABLE dbo.reserve_pots(reserve_pot_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(140) NOT NULL, allocated_amount decimal(18,2) NOT NULL DEFAULT 0, default_monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, intended_monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, funding_frequency nvarchar(30) NOT NULL DEFAULT 'Monthly', expected_funding_day int NULL, carry_forward_shortfalls bit NOT NULL DEFAULT 1, carry_excess_forward bit NOT NULL DEFAULT 0, funding_paused_from date NULL, funding_paused_until date NULL, funding_pause_reason nvarchar(300) NULL, target_amount decimal(18,2) NULL, due_date date NULL, priority int NOT NULL DEFAULT 1, is_active bit NOT NULL DEFAULT 1, notes nvarchar(500) NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF COL_LENGTH('dbo.reserve_pots','intended_monthly_contribution') IS NULL ALTER TABLE dbo.reserve_pots ADD intended_monthly_contribution decimal(18,2) NOT NULL CONSTRAINT DF_reserve_pots_intended_monthly_contribution DEFAULT 0;
IF COL_LENGTH('dbo.reserve_pots','funding_frequency') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_frequency nvarchar(30) NOT NULL CONSTRAINT DF_reserve_pots_funding_frequency DEFAULT 'Monthly';
IF COL_LENGTH('dbo.reserve_pots','expected_funding_day') IS NULL ALTER TABLE dbo.reserve_pots ADD expected_funding_day int NULL;
IF COL_LENGTH('dbo.reserve_pots','carry_forward_shortfalls') IS NULL ALTER TABLE dbo.reserve_pots ADD carry_forward_shortfalls bit NOT NULL CONSTRAINT DF_reserve_pots_carry_forward_shortfalls DEFAULT 1;
IF COL_LENGTH('dbo.reserve_pots','carry_excess_forward') IS NULL ALTER TABLE dbo.reserve_pots ADD carry_excess_forward bit NOT NULL CONSTRAINT DF_reserve_pots_carry_excess_forward DEFAULT 0;
IF COL_LENGTH('dbo.reserve_pots','funding_paused_from') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_paused_from date NULL;
IF COL_LENGTH('dbo.reserve_pots','funding_paused_until') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_paused_until date NULL;
IF COL_LENGTH('dbo.reserve_pots','funding_pause_reason') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_pause_reason nvarchar(300) NULL;
IF COL_LENGTH('dbo.reserve_pots','funding_plan_start_date') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_plan_start_date date NULL;

UPDATE p
SET funding_plan_start_date = COALESCE(
    (SELECT MIN(CAST(s.[date] AS date)) FROM dbo.savings s WHERE s.[name] = p.[name] AND s.amount > 0),
    CAST('2026-01-01' AS date))
FROM dbo.reserve_pots p
WHERE p.funding_plan_start_date IS NULL;

IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NULL
CREATE TABLE dbo.reserve_pot_monthly_funding(
    reserve_pot_monthly_funding_id int IDENTITY(1,1) PRIMARY KEY,
    reserve_pot_id int NOT NULL,
    [year] int NOT NULL,
    [month] int NOT NULL,
    expected_amount decimal(18,2) NOT NULL DEFAULT 0,
    actual_amount decimal(18,2) NOT NULL DEFAULT 0,
    [status] nvarchar(40) NOT NULL DEFAULT 'Pending',
    is_paused bit NOT NULL DEFAULT 0,
    reviewed_at datetime2 NULL,
    note nvarchar(500) NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_reserve_pot_monthly_funding_pot FOREIGN KEY(reserve_pot_id) REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE CASCADE,
    CONSTRAINT UQ_reserve_pot_monthly_funding UNIQUE(reserve_pot_id,[year],[month])
);

IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','applied_to_current_month') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD applied_to_current_month decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_current DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','applied_to_recovery') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD applied_to_recovery decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_recovery DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','carried_excess_used') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD carried_excess_used decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_credit_used DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','carried_excess_created') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD carried_excess_created decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_credit_created DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','shortfall_amount') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD shortfall_amount decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_shortfall DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','genuine_excess') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD genuine_excess decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_genuine_excess DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','recovery_balance') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD recovery_balance decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_recovery_balance DEFAULT 0;
IF OBJECT_ID('dbo.reserve_pot_monthly_funding','U') IS NOT NULL AND COL_LENGTH('dbo.reserve_pot_monthly_funding','carried_excess_balance') IS NULL ALTER TABLE dbo.reserve_pot_monthly_funding ADD carried_excess_balance decimal(18,2) NOT NULL CONSTRAINT DF_reserve_month_credit_balance DEFAULT 0;

IF OBJECT_ID('dbo.reserve_pot_recovery_allocations','U') IS NULL
EXEC(N'CREATE TABLE dbo.reserve_pot_recovery_allocations(
    reserve_pot_recovery_allocation_id int IDENTITY(1,1) PRIMARY KEY,
    reserve_pot_id int NOT NULL,
    source_year int NOT NULL,
    source_month int NOT NULL,
    target_year int NOT NULL,
    target_month int NOT NULL,
    amount decimal(18,2) NOT NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_reserve_pot_recovery_allocations_pot FOREIGN KEY(reserve_pot_id) REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE CASCADE,
    CONSTRAINT CK_reserve_pot_recovery_allocations_amount CHECK(amount > 0)
)');

IF OBJECT_ID('dbo.reserve_pot_recovery_allocations','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_reserve_pot_recovery_allocations_pot_target' AND object_id=OBJECT_ID('dbo.reserve_pot_recovery_allocations'))
EXEC(N'CREATE INDEX IX_reserve_pot_recovery_allocations_pot_target ON dbo.reserve_pot_recovery_allocations(reserve_pot_id,target_year,target_month)');

IF OBJECT_ID('dbo.reserve_pot_recovery_allocations','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_reserve_pot_recovery_allocations_pot_source' AND object_id=OBJECT_ID('dbo.reserve_pot_recovery_allocations'))
EXEC(N'CREATE INDEX IX_reserve_pot_recovery_allocations_pot_source ON dbo.reserve_pot_recovery_allocations(reserve_pot_id,source_year,source_month)');

IF OBJECT_ID('dbo.finance_events','U') IS NULL
CREATE TABLE dbo.finance_events(
    finance_event_id bigint IDENTITY(1,1) PRIMARY KEY,
    occurred_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    area nvarchar(80) NOT NULL,
    event_type nvarchar(100) NOT NULL,
    entity_type nvarchar(80) NOT NULL,
    entity_id int NULL,
    title nvarchar(220) NOT NULL,
    [description] nvarchar(1000) NULL,
    amount decimal(18,2) NULL,
    source nvarchar(40) NOT NULL DEFAULT 'System'
);

IF OBJECT_ID('dbo.recommendation_applications','U') IS NULL
CREATE TABLE dbo.recommendation_applications(
    recommendation_application_id bigint IDENTITY(1,1) PRIMARY KEY,
    operation_key nvarchar(180) NOT NULL,
    reserve_pot_id int NOT NULL,
    amount decimal(18,2) NOT NULL,
    applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UX_recommendation_applications_operation UNIQUE(operation_key),
    CONSTRAINT FK_recommendation_applications_reserve_pot FOREIGN KEY(reserve_pot_id) REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE CASCADE
);


IF OBJECT_ID('dbo.reserve_pot_actions','U') IS NULL
CREATE TABLE dbo.reserve_pot_actions(
    reserve_pot_action_id bigint IDENTITY(1,1) PRIMARY KEY,
    operation_key nvarchar(180) NOT NULL,
    reserve_pot_id int NOT NULL,
    action_type nvarchar(40) NOT NULL,
    amount decimal(18,2) NOT NULL,
    action_date date NOT NULL,
    reason nvarchar(500) NULL,
    resulting_balance decimal(18,2) NOT NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UX_reserve_pot_actions_operation UNIQUE(operation_key),
    CONSTRAINT FK_reserve_pot_actions_pot FOREIGN KEY(reserve_pot_id) REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE CASCADE
);

IF OBJECT_ID('dbo.finance_reminders','U') IS NULL
CREATE TABLE dbo.finance_reminders(
    finance_reminder_id int IDENTITY(1,1) PRIMARY KEY,
    reserve_pot_id int NULL,
    title nvarchar(220) NOT NULL,
    [description] nvarchar(1000) NULL,
    due_date date NOT NULL,
    reminder_type nvarchar(60) NOT NULL DEFAULT 'Manual',
    [status] nvarchar(30) NOT NULL DEFAULT 'Open',
    is_system_generated bit NOT NULL DEFAULT 0,
    system_key nvarchar(180) NULL,
    snoozed_until date NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_finance_reminders_reserve_pot FOREIGN KEY(reserve_pot_id) REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE SET NULL
);
IF OBJECT_ID('dbo.finance_reminders','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_finance_reminders_status_due' AND object_id=OBJECT_ID('dbo.finance_reminders'))
CREATE INDEX IX_finance_reminders_status_due ON dbo.finance_reminders([status],due_date);
IF OBJECT_ID('dbo.finance_reminders','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_finance_reminders_system_key' AND object_id=OBJECT_ID('dbo.finance_reminders'))
CREATE UNIQUE INDEX UX_finance_reminders_system_key ON dbo.finance_reminders(system_key) WHERE system_key IS NOT NULL;



IF OBJECT_ID('dbo.saving_pots','U') IS NULL
CREATE TABLE dbo.saving_pots(saving_pot_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(120) NOT NULL, target_amount decimal(18,2) NOT NULL, monthly_amount decimal(18,2) NOT NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF COL_LENGTH('dbo.saving_pots','priority') IS NULL ALTER TABLE dbo.saving_pots ADD [priority] int NOT NULL DEFAULT 1;
IF COL_LENGTH('dbo.saving_pots','pot_type') IS NULL ALTER TABLE dbo.saving_pots ADD pot_type nvarchar(80) NOT NULL DEFAULT 'Goal';
IF COL_LENGTH('dbo.saving_pots','access_speed') IS NULL ALTER TABLE dbo.saving_pots ADD access_speed nvarchar(80) NOT NULL DEFAULT 'Immediate';
IF COL_LENGTH('dbo.saving_pots','interest_rate') IS NULL ALTER TABLE dbo.saving_pots ADD interest_rate decimal(9,4) NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.saving_pots','target_date') IS NULL ALTER TABLE dbo.saving_pots ADD target_date date NULL;
IF COL_LENGTH('dbo.saving_pots','destination') IS NULL ALTER TABLE dbo.saving_pots ADD destination nvarchar(160) NULL;
IF COL_LENGTH('dbo.saving_pots','contribution_mode') IS NULL ALTER TABLE dbo.saving_pots ADD contribution_mode nvarchar(80) NOT NULL CONSTRAINT DF_saving_pots_contribution_mode DEFAULT 'Uses global savings schedule';

IF OBJECT_ID('dbo.saving_pot_months','U') IS NULL
CREATE TABLE dbo.saving_pot_months(saving_pot_month_id int IDENTITY(1,1) PRIMARY KEY, saving_pot_id int NOT NULL, [year] int NOT NULL, [month] int NOT NULL, is_saved bit NOT NULL DEFAULT 0, saved_amount decimal(18,2) NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_saving_pot_months_pots FOREIGN KEY(saving_pot_id) REFERENCES dbo.saving_pots(saving_pot_id) ON DELETE CASCADE, CONSTRAINT UQ_saving_pot_months UNIQUE(saving_pot_id,[year],[month]));

IF OBJECT_ID('dbo.saving_pot_months','U') IS NOT NULL AND COL_LENGTH('dbo.saving_pot_months', 'saved_amount') IS NULL
ALTER TABLE dbo.saving_pot_months ADD saved_amount decimal(18,2) NOT NULL DEFAULT 0;

IF OBJECT_ID('dbo.saving_pot_extras','U') IS NULL
CREATE TABLE dbo.saving_pot_extras(
    saving_pot_extra_id int IDENTITY(1,1) PRIMARY KEY,
    saving_pot_id int NOT NULL,
    amount decimal(18,2) NOT NULL,
    [date] date NOT NULL DEFAULT CONVERT(date, GETDATE()),
    note nvarchar(250) NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_saving_pot_extras_pots FOREIGN KEY(saving_pot_id) REFERENCES dbo.saving_pots(saving_pot_id) ON DELETE CASCADE
);

IF OBJECT_ID('dbo.savings_contribution_changes','U') IS NULL
CREATE TABLE dbo.savings_contribution_changes(
    savings_contribution_change_id int IDENTITY(1,1) PRIMARY KEY,
    starts_on date NOT NULL,
    monthly_amount decimal(18,2) NOT NULL,
    note nvarchar(250) NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

UPDATE m
SET saved_amount = p.monthly_amount
FROM dbo.saving_pot_months m
INNER JOIN dbo.saving_pots p ON p.saving_pot_id = m.saving_pot_id
WHERE m.is_saved = 1
  AND m.saved_amount = 0;


IF OBJECT_ID('dbo.asset_holdings','U') IS NULL
CREATE TABLE dbo.asset_holdings(
    asset_holding_id int IDENTITY(1,1) PRIMARY KEY,
    [name] nvarchar(160) NOT NULL,
    asset_type nvarchar(40) NOT NULL DEFAULT 'Manual',
    symbol nvarchar(40) NULL,
    quantity decimal(28,8) NOT NULL DEFAULT 0,
    average_buy_price decimal(18,4) NULL,
    current_price decimal(18,4) NULL,
    current_value decimal(18,2) NULL,
    currency nvarchar(10) NOT NULL DEFAULT 'GBP',
    use_live_price bit NOT NULL DEFAULT 0,
    provider nvarchar(60) NULL,
    broker nvarchar(80) NULL,
    price_source nvarchar(40) NOT NULL DEFAULT 'Auto',
    valuation_method nvarchar(40) NOT NULL DEFAULT 'SpotPremium',
    metal_weight_oz decimal(18,8) NULL,
    metal_purity decimal(9,4) NULL,
    premium_value decimal(18,2) NULL,
    metal_year int NULL,
    bullion_series nvarchar(100) NULL,
    bullion_form nvarchar(30) NULL,
    manual_value decimal(18,2) NULL,
    annual_growth_rate decimal(9,4) NULL,
    monthly_contribution decimal(18,2) NULL,
    purchase_date date NULL,
    last_price_updated_at datetime2 NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','valuation_method') IS NULL ALTER TABLE dbo.asset_holdings ADD valuation_method nvarchar(40) NOT NULL CONSTRAINT DF_asset_holdings_valuation_method DEFAULT 'SpotPremium';
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','metal_weight_oz') IS NULL ALTER TABLE dbo.asset_holdings ADD metal_weight_oz decimal(18,8) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','metal_purity') IS NULL ALTER TABLE dbo.asset_holdings ADD metal_purity decimal(9,4) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','premium_value') IS NULL ALTER TABLE dbo.asset_holdings ADD premium_value decimal(18,2) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','metal_year') IS NULL ALTER TABLE dbo.asset_holdings ADD metal_year int NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','bullion_series') IS NULL ALTER TABLE dbo.asset_holdings ADD bullion_series nvarchar(100) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','bullion_form') IS NULL ALTER TABLE dbo.asset_holdings ADD bullion_form nvarchar(30) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','manual_value') IS NULL ALTER TABLE dbo.asset_holdings ADD manual_value decimal(18,2) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','annual_growth_rate') IS NULL ALTER TABLE dbo.asset_holdings ADD annual_growth_rate decimal(9,4) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','monthly_contribution') IS NULL ALTER TABLE dbo.asset_holdings ADD monthly_contribution decimal(18,2) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','purchase_date') IS NULL ALTER TABLE dbo.asset_holdings ADD purchase_date date NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','provider') IS NULL ALTER TABLE dbo.asset_holdings ADD provider nvarchar(60) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','broker') IS NULL ALTER TABLE dbo.asset_holdings ADD broker nvarchar(80) NULL;
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND COL_LENGTH('dbo.asset_holdings','price_source') IS NULL ALTER TABLE dbo.asset_holdings ADD price_source nvarchar(40) NOT NULL CONSTRAINT DF_asset_holdings_price_source DEFAULT 'Auto';
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL UPDATE dbo.asset_holdings SET broker = COALESCE(NULLIF(broker,''), NULLIF(provider,''), broker), price_source = COALESCE(NULLIF(price_source,''), 'Auto'), valuation_method = COALESCE(NULLIF(valuation_method,''), CASE WHEN bullion_form IN ('Proof Coin','Commemorative Coin','Coin Set','Medal') THEN 'Manual' ELSE 'SpotPremium' END), metal_purity = CASE WHEN asset_type = 'Gold' AND metal_purity IS NULL THEN 999.9 WHEN asset_type = 'Silver' AND metal_purity IS NULL THEN 999 ELSE metal_purity END;
-- Manual proof/collectible coin valuation: avoid valuing proof coins only by melt value.
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL
UPDATE dbo.asset_holdings
SET valuation_method = 'Manual',
    use_live_price = 0,
    manual_value = COALESCE(manual_value, current_value, ROUND(quantity * ISNULL(average_buy_price, 0), 2)),
    current_value = COALESCE(manual_value, current_value, ROUND(quantity * ISNULL(average_buy_price, 0), 2))
WHERE asset_type IN ('Gold','Silver')
  AND bullion_form IN ('Proof Coin','Commemorative Coin','Coin Set','Medal')
  AND ISNULL(valuation_method,'SpotPremium') IN ('SpotPremium','Spot');

IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_asset_holdings_type' AND object_id=OBJECT_ID('dbo.asset_holdings')) CREATE INDEX IX_asset_holdings_type ON dbo.asset_holdings(asset_type);
IF OBJECT_ID('dbo.asset_holdings','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_asset_holdings_symbol' AND object_id=OBJECT_ID('dbo.asset_holdings')) CREATE INDEX IX_asset_holdings_symbol ON dbo.asset_holdings(symbol);

IF NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE [name]='Lucy''s ISA') INSERT INTO dbo.account_balances([name], amount, interest_rate, monthly_contribution, include_in_global_goal) VALUES('Lucy''s ISA',4000,3.8,0,1);
IF NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE [name]='Monzo Pots') INSERT INTO dbo.account_balances([name], amount, interest_rate, monthly_contribution, include_in_global_goal) VALUES('Monzo Pots',1370,2.75,0,1);";
        await ExecuteAsync(sql);
    }

    public async Task<List<PaymentRow>> GetRowsAsync(string source, int month, int year)
    {
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Id: "billid", Date: "[date]", Category: "NULL", Type: "type", Length: "length", Notes: "description"),
            "everyday_spending" => (Table: "dbo.everyday_spending", Id: "everyday_spending_id", Date: "[date]", Category: "category", Type: "type", Length: "length", Notes: "description"),
            "extra_expenses" => (Table: "dbo.extra_expenses", Id: "extra_expense_id", Date: "duedate", Category: "category", Type: "type", Length: "length", Notes: "description"),
            "investments" => (Table: "dbo.investments", Id: "investments_id", Date: "[date]", Category: "category", Type: "NULL", Length: "length", Notes: "notes"),
            "savings" => (Table: "dbo.savings", Id: "savings_id", Date: "[date]", Category: "NULL", Type: "NULL", Length: "length", Notes: "notes"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        string sql = $@"SELECT {map.Id} AS id, [name], amount, {map.Date} AS [date], {map.Category} AS category, {map.Type} AS [type], {map.Length} AS [length], {map.Notes} AS notes
FROM {map.Table} WHERE MONTH({map.Date})=@month AND YEAR({map.Date})=@year ORDER BY {map.Date} DESC";
        var rows = new List<PaymentRow>();
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@month", month); cmd.Parameters.AddWithValue("@year", year);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) rows.Add(new PaymentRow(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDateTime(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), source));
        return rows;
    }

    public async Task<decimal> GetEmergencyFundAsync()
    {
        var value = await ScalarAsync("SELECT TOP 1 amount FROM dbo.emergency_fund ORDER BY updated_at DESC");
        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    public async Task<DateTime?> GetEmergencyFundUpdatedAsync()
    {
        var value = await ScalarAsync("SELECT TOP 1 updated_at FROM dbo.emergency_fund ORDER BY updated_at DESC");
        return value is null or DBNull ? null : Convert.ToDateTime(value);
    }

    public async Task<decimal> GetMonthlyAllowanceAsync(int month, decimal fallback)
    {
        var value = await ScalarAsync("SELECT TOP 1 amount FROM dbo.monthly_allowance WHERE month_id=@month", ("@month", month));
        return value is null or DBNull ? fallback : Convert.ToDecimal(value);
    }

    public async Task<IncomeSnapshot?> GetIncomeAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT [year],[month],amount,sick_days,updated_at FROM dbo.monthly_income_stats WHERE [year]=@year AND [month]=@month", con);
        cmd.Parameters.AddWithValue("@year", year); cmd.Parameters.AddWithValue("@month", month);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? new IncomeSnapshot(r.GetInt32(0), r.GetInt32(1), r.GetDecimal(2), r.GetInt32(3), r.GetDateTime(4)) : null;
    }

    public async Task<List<AccountBalance>> GetAccountsAsync(decimal emergencyFund)
    {
        await EnsureModernTablesAsync();
        var includeEmergency = await GetDecimalSettingAsync("SavingsIncludeEmergencyFund", 1m) == 1m;
        var accounts = new List<AccountBalance>
        {
            new(0, "Emergency Fund", emergencyFund, await GetDecimalSettingAsync("EmergencyFundInterestRate", 3.8m), 0, true, await GetEmergencyFundUpdatedAsync() ?? DateTime.MinValue, includeEmergency)
        };
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT account_balance_id,[name],amount,interest_rate,monthly_contribution,include_in_global_goal,updated_at,include_in_savings_command FROM dbo.account_balances ORDER BY [name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            accounts.Add(new AccountBalance(
                r.GetInt32(0),
                r.GetString(1),
                r.GetDecimal(2),
                r.GetDecimal(3),
                r.GetDecimal(4),
                r.GetBoolean(5),
                r.GetDateTime(6),
                r.GetBoolean(7)));
        }
        return accounts;
    }

    public async Task<List<IncomeSnapshot>> GetIncomeHistoryAsync()
    {
        await EnsureModernTablesAsync(); var list = new List<IncomeSnapshot>();
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT TOP 24 [year],[month],amount,sick_days,updated_at FROM dbo.monthly_income_stats ORDER BY [year] DESC,[month] DESC", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new IncomeSnapshot(r.GetInt32(0), r.GetInt32(1), r.GetDecimal(2), r.GetInt32(3), r.GetDateTime(4)));
        return list;
    }

    public async Task SaveIncomeAsync(int year, int month, decimal amount, int sickDays)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"MERGE dbo.monthly_income_stats AS t USING (SELECT @year y,@month m) AS s ON t.[year]=s.y AND t.[month]=s.m WHEN MATCHED THEN UPDATE SET amount=@amount,sick_days=@sick,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([year],[month],amount,sick_days) VALUES(@year,@month,@amount,@sick);", ("@year", year), ("@month", month), ("@amount", amount), ("@sick", sickDays));
        await ExecuteAsync(@"MERGE dbo.monthly_allowance AS t USING (SELECT @month m) AS s ON t.month_id=s.m WHEN MATCHED THEN UPDATE SET amount=@amount WHEN NOT MATCHED THEN INSERT(month_id, amount) VALUES(@month,@amount);", ("@month", month), ("@amount", amount));
    }


    public async Task<List<SavingsContributionChange>> GetSavingsContributionChangesAsync()
    {
        await EnsureModernTablesAsync();
        var list = new List<SavingsContributionChange>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT savings_contribution_change_id, starts_on, monthly_amount, note, created_at FROM dbo.savings_contribution_changes ORDER BY starts_on", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SavingsContributionChange(
                r.GetInt32(0),
                r.GetDateTime(1),
                r.GetDecimal(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetDateTime(4)));
        }
        return list;
    }

    public async Task AddSavingsContributionChangeAsync(DateTime startsOn, decimal monthlyAmount, string? note)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("INSERT INTO dbo.savings_contribution_changes(starts_on, monthly_amount, note) VALUES(@startsOn, @monthlyAmount, @note)",
            ("@startsOn", startsOn.Date),
            ("@monthlyAmount", monthlyAmount),
            ("@note", DbValue(note)));
    }

    public async Task DeleteSavingsContributionChangeAsync(int id)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("DELETE FROM dbo.savings_contribution_changes WHERE savings_contribution_change_id=@id", ("@id", id));
    }

    public async Task SaveManualEmergencyFundUpdateAsync(decimal newTotal, string? reason)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("IF EXISTS (SELECT 1 FROM dbo.emergency_fund) UPDATE dbo.emergency_fund SET amount=@amount, updated_at=SYSUTCDATETIME() ELSE INSERT INTO dbo.emergency_fund(amount, updated_at) VALUES(@amount, SYSUTCDATETIME())", ("@amount", newTotal));
        var rate = await GetDecimalSettingAsync("EmergencyFundInterestRate", 3.8m);
        await ExecuteAsync("INSERT INTO dbo.account_balance_history(account_balance_id,[name],amount,interest_rate,monthly_contribution) VALUES(NULL,@name,@amount,@rate,0)",
            ("@name", string.IsNullOrWhiteSpace(reason) ? "Emergency Fund manual update" : $"Emergency Fund manual update - {reason.Trim()}"),
            ("@amount", newTotal),
            ("@rate", rate));
    }

    public async Task SaveSavingsSourcesAsync(bool includeEmergencyFund, IReadOnlyCollection<int> accountIds)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"MERGE dbo.finance_settings AS t USING (SELECT @key AS [key]) AS s ON t.[key]=s.[key] WHEN MATCHED THEN UPDATE SET [value]=@value, updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([key],[value]) VALUES(@key,@value);",
            ("@key", "SavingsIncludeEmergencyFund"),
            ("@value", includeEmergencyFund ? "1" : "0"));

        await ExecuteAsync("UPDATE dbo.account_balances SET include_in_savings_command = 0");
        var ids = accountIds.Distinct().Where(id => id > 0).ToArray();
        if (ids.Length == 0) return;

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        var parameterNames = ids.Select((_, index) => $"@id{index}").ToArray();
        await using var cmd = new SqlCommand($"UPDATE dbo.account_balances SET include_in_savings_command = 1 WHERE account_balance_id IN ({string.Join(",", parameterNames)})", con);
        for (var i = 0; i < ids.Length; i++)
        {
            cmd.Parameters.AddWithValue(parameterNames[i], ids[i]);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveAccountAsync(int id, string name, decimal amount, decimal rate, decimal monthly, bool include)
    {
        await EnsureModernTablesAsync();
        if (id == 0 && name == "Emergency Fund") { await ExecuteAsync("IF EXISTS (SELECT 1 FROM dbo.emergency_fund) UPDATE dbo.emergency_fund SET amount=@amount,updated_at=GETDATE() ELSE INSERT INTO dbo.emergency_fund(amount,updated_at) VALUES(@amount,GETDATE())", ("@amount", amount)); await ExecuteAsync("MERGE dbo.finance_settings AS t USING (SELECT @key AS [key]) AS s ON t.[key]=s.[key] WHEN MATCHED THEN UPDATE SET [value]=@value, updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([key],[value]) VALUES(@key,@value);", ("@key", "EmergencyFundInterestRate"), ("@value", rate)); return; }
        if (id == 0) await ExecuteAsync("INSERT INTO dbo.account_balances([name],amount,interest_rate,monthly_contribution,include_in_global_goal) VALUES(@name,@amount,@rate,@monthly,@include)", ("@name", name), ("@amount", amount), ("@rate", rate), ("@monthly", monthly), ("@include", include));
        else await ExecuteAsync("UPDATE dbo.account_balances SET [name]=@name,amount=@amount,interest_rate=@rate,monthly_contribution=@monthly,include_in_global_goal=@include,updated_at=SYSUTCDATETIME() WHERE account_balance_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@rate", rate), ("@monthly", monthly), ("@include", include));
        await ExecuteAsync("INSERT INTO dbo.account_balance_history(account_balance_id,[name],amount,interest_rate,monthly_contribution) VALUES(@id,@name,@amount,@rate,@monthly)", ("@id", id), ("@name", name), ("@amount", amount), ("@rate", rate), ("@monthly", monthly));
    }


    public async Task DeleteAccountAsync(int id)
    {
        await EnsureModernTablesAsync();
        if (id == 0)
        {
            await ExecuteAsync("UPDATE dbo.emergency_fund SET amount=0, updated_at=SYSUTCDATETIME()");
            return;
        }
        await ExecuteAsync("DELETE FROM dbo.account_balances WHERE account_balance_id=@id", ("@id", id));
    }

    public async Task DeletePaymentAsync(string source, int id)
    {
        await EnsureModernTablesAsync();
        var sql = source switch
        {
            "bills" => "DELETE FROM dbo.bills WHERE billid=@id",
            "everyday_spending" => "DELETE FROM dbo.everyday_spending WHERE everyday_spending_id=@id",
            "extra_expenses" => "DELETE FROM dbo.extra_expenses WHERE extra_expense_id=@id",
            "investments" => "DELETE FROM dbo.investments WHERE investments_id=@id",
            "savings" => "DELETE FROM dbo.savings WHERE savings_id=@id",
            _ => throw new ArgumentOutOfRangeException(nameof(source), "Unknown payment section.")
        };
        await ExecuteAsync(sql, ("@id", id));
    }

    public async Task SaveStocksCryptoAsync(decimal amount, decimal rate, decimal monthly)
    {
        await SaveDecimalSettingAsync("StocksCryptoValue", amount);
        await SaveDecimalSettingAsync("StocksCryptoInterestRate", rate);
        await SaveDecimalSettingAsync("StocksCryptoMonthlyContribution", monthly);
    }

    public async Task<(decimal Amount, decimal Rate, decimal Monthly)> GetStocksCryptoAsync()
    {
        return (
            await GetDecimalSettingAsync("StocksCryptoValue", 0m),
            await GetDecimalSettingAsync("StocksCryptoInterestRate", 0m),
            await GetDecimalSettingAsync("StocksCryptoMonthlyContribution", 0m)
        );
    }

    public async Task AddPaymentAsync(string source, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        switch (source)
        {
            case "bills":
                await ExecuteAsync("INSERT INTO dbo.bills([name], amount, [date], [type], [length], [description]) VALUES(@name,@amount,@date,@type,@length,@notes)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "everyday_spending":
                await ExecuteAsync("INSERT INTO dbo.everyday_spending([name], amount, [date], category, [type], [length], [description]) VALUES(@name,@amount,@date,@category,@type,@length,@notes)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "extra_expenses":
                await ExecuteAsync("INSERT INTO dbo.extra_expenses([name], amount, duedate, category, [type], [length], [description]) VALUES(@name,@amount,@date,@category,@type,@length,@notes)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "investments":
                await ExecuteAsync("INSERT INTO dbo.investments([name], amount, [date], category, [length], notes) VALUES(@name,@amount,@date,@category,@length,@notes)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "savings":
                await ExecuteAsync("INSERT INTO dbo.savings([name], amount, [date], [length], notes) VALUES(@name,@amount,@date,@length,@notes)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), "Unknown payment section.");
        }
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    public async Task<decimal> CarryOverAsync(int year, int month, string[] sections)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        const string autoNote = "Automatically carried from previous month";

        await ExecuteAsync("""
    DELETE FROM dbo.extra_expenses
    WHERE MONTH(duedate) = @toMonth
      AND YEAR(duedate) = @toYear
      AND [name] = 'Shortfall carried forward'
""", ("@toMonth", to.Month), ("@toYear", to.Year));

        await ExecuteAsync("""
    DELETE FROM dbo.savings
    WHERE MONTH([date]) = @toMonth
      AND YEAR([date]) = @toYear
      AND [name] = 'Carried forward surplus'
""", ("@toMonth", to.Month), ("@toYear", to.Year));

        if (sections.Contains("bills"))
        {
            await ExecuteAsync("""
        DELETE target
        FROM dbo.bills target
        INNER JOIN dbo.bills source ON source.[name] = target.[name]
        WHERE MONTH(source.[date]) = @month
          AND YEAR(source.[date]) = @year
          AND MONTH(target.[date]) = @toMonth
          AND YEAR(target.[date]) = @toYear
    """, ("@month", month), ("@year", year), ("@toMonth", to.Month), ("@toYear", to.Year));

            await ExecuteAsync("""
        INSERT INTO dbo.bills([name], amount, [date], [type], [length], [description])
        SELECT [name], amount, @toDate, [type],
               CASE
                   WHEN TRY_CONVERT(int, [length]) IS NOT NULL AND TRY_CONVERT(int, [length]) > 1
                       THEN CONVERT(nvarchar(50), TRY_CONVERT(int, [length]) - 1)
                   ELSE [length]
               END,
               @autoNote
        FROM dbo.bills
        WHERE MONTH([date]) = @month
          AND YEAR([date]) = @year
    """, ("@toDate", to), ("@month", month), ("@year", year), ("@autoNote", autoNote));
        }

        if (sections.Contains("investments"))
        {
            await ExecuteAsync("""
        DELETE target
        FROM dbo.investments target
        INNER JOIN dbo.investments source ON source.[name] = target.[name]
        WHERE MONTH(source.[date]) = @month
          AND YEAR(source.[date]) = @year
          AND MONTH(target.[date]) = @toMonth
          AND YEAR(target.[date]) = @toYear
    """, ("@month", month), ("@year", year), ("@toMonth", to.Month), ("@toYear", to.Year));

            await ExecuteAsync("""
        INSERT INTO dbo.investments([name], amount, [date], category, [length], notes)
        SELECT [name], amount, @toDate, category,
               CASE
                   WHEN TRY_CONVERT(int, [length]) IS NOT NULL AND TRY_CONVERT(int, [length]) > 1
                       THEN CONVERT(nvarchar(50), TRY_CONVERT(int, [length]) - 1)
                   ELSE [length]
               END,
               @autoNote
        FROM dbo.investments
        WHERE MONTH([date]) = @month
          AND YEAR([date]) = @year
    """, ("@toDate", to), ("@month", month), ("@year", year), ("@autoNote", autoNote));
        }

        if (sections.Contains("extra_expenses"))
        {
            await ExecuteAsync("""
        DELETE target
        FROM dbo.extra_expenses target
        INNER JOIN dbo.extra_expenses source ON source.[name] = target.[name]
        WHERE MONTH(source.duedate) = @month
          AND YEAR(source.duedate) = @year
          AND MONTH(target.duedate) = @toMonth
          AND YEAR(target.duedate) = @toYear
    """, ("@month", month), ("@year", year), ("@toMonth", to.Month), ("@toYear", to.Year));

            await ExecuteAsync("""
        INSERT INTO dbo.extra_expenses([name], amount, duedate, category, [type], [length], [description])
        SELECT [name], amount, @toDate, category, [type],
               CASE
                   WHEN TRY_CONVERT(int, [length]) IS NOT NULL AND TRY_CONVERT(int, [length]) > 1
                       THEN CONVERT(nvarchar(50), TRY_CONVERT(int, [length]) - 1)
                   ELSE [length]
               END,
               @autoNote
        FROM dbo.extra_expenses
        WHERE MONTH(duedate) = @month
          AND YEAR(duedate) = @year
    """, ("@toDate", to), ("@month", month), ("@year", year), ("@autoNote", autoNote));
        }

        var monthlyIncomeObj = await ScalarAsync("""
    SELECT TOP 1 amount
    FROM dbo.monthly_income_stats
    WHERE [year] = @year AND [month] = @month
""", ("@year", year), ("@month", month));

        decimal monthlyIncome = monthlyIncomeObj is null || monthlyIncomeObj is DBNull
            ? await GetMonthlyAllowanceAsync(
                month,
                decimal.TryParse(config["FinanceSettings:DefaultMonthlyIncome"], out var d) ? d : 3500m)
            : Convert.ToDecimal(monthlyIncomeObj);

        var billsTotalObj = await ScalarAsync("""
    SELECT SUM(amount)
    FROM dbo.bills
    WHERE MONTH([date]) = @month AND YEAR([date]) = @year
""", ("@month", month), ("@year", year));

        var expensesTotalObj = await ScalarAsync("""
    SELECT SUM(amount)
    FROM dbo.extra_expenses
    WHERE MONTH(duedate) = @month AND YEAR(duedate) = @year
""", ("@month", month), ("@year", year));

        var investmentsTotalObj = await ScalarAsync("""
    SELECT SUM(amount)
    FROM dbo.investments
    WHERE MONTH([date]) = @month AND YEAR([date]) = @year
""", ("@month", month), ("@year", year));

        var savingsTotalObj = await ScalarAsync("""
    SELECT SUM(amount)
    FROM dbo.savings
    WHERE MONTH([date]) = @month AND YEAR([date]) = @year
""", ("@month", month), ("@year", year));

        decimal billsTotal = billsTotalObj is null || billsTotalObj is DBNull ? 0m : Convert.ToDecimal(billsTotalObj);
        decimal expensesTotal = expensesTotalObj is null || expensesTotalObj is DBNull ? 0m : Convert.ToDecimal(expensesTotalObj);
        decimal investmentsTotal = investmentsTotalObj is null || investmentsTotalObj is DBNull ? 0m : Convert.ToDecimal(investmentsTotalObj);
        decimal savingsTotal = savingsTotalObj is null || savingsTotalObj is DBNull ? 0m : Convert.ToDecimal(savingsTotalObj);

        var grandOutgoings = billsTotal + expensesTotal + investmentsTotal;
        var remainingFund = monthlyIncome - grandOutgoings + savingsTotal;

        var monthlyTarget = decimal.TryParse(config["FinanceSettings:MonthlySavingTarget"], out var mt)
            ? mt
            : 1200m;

        var carryAmount = Math.Round(remainingFund - monthlyTarget, 2);

        if (carryAmount < 0)
        {
            await ExecuteAsync("""
        INSERT INTO dbo.extra_expenses([name], amount, duedate, category, [type], [length], [description])
        VALUES(@name, @amount, @date, @category, @type, @length, @notes)
    """,
            ("@name", "Shortfall carried forward"),
            ("@amount", Math.Abs(carryAmount)),
            ("@date", to),
            ("@category", "Shortfall"),
            ("@type", DBNull.Value),
            ("@length", DBNull.Value),
            ("@notes", autoNote));
        }
        else if (carryAmount > 0)
        {
            await ExecuteAsync("""
        INSERT INTO dbo.savings([name], amount, [date], [length], notes)
        VALUES(@name, @amount, @date, @length, @notes)
    """,
            ("@name", "Carried forward surplus"),
            ("@amount", carryAmount),
            ("@date", to),
            ("@length", DBNull.Value),
            ("@notes", autoNote));
        }

        return carryAmount;
    }


    public async Task<List<ExistingPaymentOption>> GetExistingPaymentOptionsAsync(string source)
    {
        await EnsureModernTablesAsync();
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Date: "[date]", Category: "NULL", Type: "[type]", Length: "[length]", Notes: "[description]"),
            "everyday_spending" => (Table: "dbo.everyday_spending", Date: "[date]", Category: "category", Type: "[type]", Length: "[length]", Notes: "[description]"),
            "extra_expenses" => (Table: "dbo.extra_expenses", Date: "duedate", Category: "category", Type: "[type]", Length: "[length]", Notes: "[description]"),
            "investments" => (Table: "dbo.investments", Date: "[date]", Category: "category", Type: "NULL", Length: "[length]", Notes: "notes"),
            "savings" => (Table: "dbo.savings", Date: "[date]", Category: "NULL", Type: "NULL", Length: "[length]", Notes: "notes"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        var sql = $"""
WITH ranked AS
(
    SELECT [name], amount, {map.Category} AS category, {map.Type} AS [type],
           {map.Length} AS [length], {map.Notes} AS notes,
           ROW_NUMBER() OVER(PARTITION BY [name] ORDER BY {map.Date} DESC) AS rn
    FROM {map.Table}
)
SELECT [name], amount, category, [type], [length], notes
FROM ranked
WHERE rn = 1
ORDER BY [name];
""";
        var items = new List<ExistingPaymentOption>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            items.Add(new ExistingPaymentOption(
                r.GetString(0),
                r.GetDecimal(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5)));
        }
        return items;
    }

    public async Task<List<CarryOverItemInput>> GetCarryOverItemsAsync(int year, int month)
    {
        var items = new List<CarryOverItemInput>();
        foreach (var source in new[] { "bills", "everyday_spending", "investments", "savings" })
        {
            var rows = await GetRowsAsync(source, month, year);
            items.AddRange(rows.Select(x => new CarryOverItemInput
            {
                Include = true,
                Source = source,
                SourceId = x.Id,
                Name = x.Name,
                Amount = x.Amount,
                Category = x.Category,
                Type = x.Type,
                Length = x.Length,
                Notes = x.Notes
            }));
        }
        return items;
    }

    public async Task<CarryForwardInfo> GetCarryForwardInfoAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        await using var connection = new SqlConnection(ConnStr);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"SELECT amount, override_amount, override_reason
FROM dbo.monthly_carry_forward
WHERE [year]=@year AND [month]=@month", connection);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return new CarryForwardInfo(0m, null, null);
        var calculated = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
        decimal? overrideAmount = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
        var reason = reader.IsDBNull(2) ? null : reader.GetString(2);
        return new CarryForwardInfo(calculated, overrideAmount, reason);
    }

    public async Task<decimal> GetCarryForwardAsync(int year, int month)
        => (await GetCarryForwardInfoAsync(year, month)).EffectiveAmount;

    public async Task SaveCarryForwardOverrideAsync(int year, int month, decimal amount, string? reason)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"MERGE dbo.monthly_carry_forward AS target
USING (SELECT @year AS [year], @month AS [month]) AS source
ON target.[year]=source.[year] AND target.[month]=source.[month]
WHEN MATCHED THEN UPDATE SET override_amount=@amount, override_reason=@reason, updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT([year],[month],amount,override_amount,override_reason) VALUES(@year,@month,0,@amount,@reason);",
            ("@year", year), ("@month", month), ("@amount", amount), ("@reason", string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim()));
    }

    public async Task ClearCarryForwardOverrideAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("UPDATE dbo.monthly_carry_forward SET override_amount=NULL, override_reason=NULL, updated_at=SYSUTCDATETIME() WHERE [year]=@year AND [month]=@month",
            ("@year", year), ("@month", month));
    }

    public async Task<decimal> GetMonthResultAsync(int year, int month)
    {
        await EnsureModernTablesAsync();

        var income = await GetIncomeAsync(year, month);
        var fallbackIncome = decimal.TryParse(config["FinanceSettings:DefaultMonthlyIncome"], out var configuredIncome) ? configuredIncome : 3600m;
        var monthlyIncome = income?.Amount ?? await GetMonthlyAllowanceAsync(month, fallbackIncome);
        var carryForward = await GetCarryForwardAsync(year, month);

        var bills = await GetRowsAsync("bills", month, year);
        var everyday = await GetRowsAsync("everyday_spending", month, year);
        var extras = await GetRowsAsync("extra_expenses", month, year);
        var investments = await GetRowsAsync("investments", month, year);
        var savings = await GetRowsAsync("savings", month, year);

        var allocated = bills.Sum(x => x.Amount)
            + everyday.Sum(x => x.Amount)
            + extras.Sum(x => x.Amount)
            + investments.Sum(x => x.Amount)
            + savings.Sum(x => x.Amount);

        return Math.Round(monthlyIncome + carryForward - allocated, 2);
    }

    private async Task SaveCarryForwardAsync(int year, int month, decimal amount, int sourceYear, int sourceMonth)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"MERGE dbo.monthly_carry_forward AS target
USING (SELECT @year AS [year], @month AS [month]) AS source
ON target.[year]=source.[year] AND target.[month]=source.[month]
WHEN MATCHED THEN UPDATE SET amount=@amount, source_year=@sourceYear, source_month=@sourceMonth, updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT([year],[month],amount,source_year,source_month) VALUES(@year,@month,@amount,@sourceYear,@sourceMonth);",
            ("@year", year), ("@month", month), ("@amount", amount), ("@sourceYear", sourceYear), ("@sourceMonth", sourceMonth));
    }

    public async Task<decimal> CopyConfirmedItemsAsync(int year, int month, IEnumerable<CarryOverItemInput> items)
    {
        var to = new DateTime(year, month, 1).AddMonths(1);
        var monthResult = await GetMonthResultAsync(year, month);

        // Carry over is a replacement operation. Remove any recurring data that
        // already exists in the destination month before adding the confirmed set.
        // Extra expenses are deliberately preserved because they are one-off items
        // and are excluded from carry over.
        await OverwriteCarryOverMonthAsync(to.Year, to.Month);

        foreach (var item in items.Where(x => x.Include && x.Amount >= 0 && !string.IsNullOrWhiteSpace(x.Name)))
        {
            await AddPaymentAsync(item.Source, item.Name.Trim(), item.Amount, to, item.Category, item.Type, item.Length, "Carried over and confirmed from previous month");
            if (item.Source == "savings" && item.Amount > 0)
                await ApplyReserveAllocationAsync(item.Name, item.Amount);
        }

        await SyncAllReservePotContributionAveragesAsync();
        await SaveCarryForwardAsync(to.Year, to.Month, monthResult, year, month);
        return monthResult;
    }

    private async Task OverwriteCarryOverMonthAsync(int year, int month)
    {
        // Reverse any reserve allocations already recorded for the destination
        // month so replacing the month does not double-count virtual pot balances.
        var existingReserveAllocations = await GetRowsAsync("savings", month, year);
        foreach (var allocation in existingReserveAllocations.Where(x => x.Amount > 0))
            await ApplyReserveAllocationAsync(allocation.Name, -allocation.Amount);

        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        await ExecuteAsync("DELETE FROM dbo.bills WHERE [date] >= @start AND [date] < @end", ("@start", start), ("@end", end));
        await ExecuteAsync("DELETE FROM dbo.everyday_spending WHERE [date] >= @start AND [date] < @end", ("@start", start), ("@end", end));
        await ExecuteAsync("DELETE FROM dbo.investments WHERE [date] >= @start AND [date] < @end", ("@start", start), ("@end", end));
        await ExecuteAsync("DELETE FROM dbo.savings WHERE [date] >= @start AND [date] < @end", ("@start", start), ("@end", end));
    }

    public async Task ApplyReserveAllocationAsync(string name, decimal amount)
    {
        if (amount == 0 || string.IsNullOrWhiteSpace(name)) return;
        await EnsureModernTablesAsync();
        await ExecuteAsync("""
MERGE dbo.reserve_pots AS target
USING (SELECT @name AS [name]) AS source
ON target.[name] = source.[name]
WHEN MATCHED THEN
    UPDATE SET allocated_amount = CASE WHEN allocated_amount + @amount < 0 THEN 0 ELSE allocated_amount + @amount END,
               updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT([name], allocated_amount, default_monthly_contribution, priority, is_active, notes)
    VALUES(@name, CASE WHEN @amount < 0 THEN 0 ELSE @amount END, 0, 10, 1, 'Created from a dashboard household reserve allocation');
""", ("@name", name.Trim()), ("@amount", amount));

        await ExecuteAsync("""
UPDATE dbo.household_reserve
SET balance = CASE WHEN balance + @amount < 0 THEN 0 ELSE balance + @amount END,
    updated_at = SYSUTCDATETIME()
WHERE household_reserve_id = 1;
""", ("@amount", amount));

        await SyncReservePotContributionAverageAsync(name.Trim());
        var potIdObj = await ScalarAsync("SELECT TOP 1 reserve_pot_id FROM dbo.reserve_pots WHERE [name]=@name", ("@name", name.Trim()));
        if (potIdObj is not null && potIdObj != DBNull.Value)
        {
            var potId = Convert.ToInt32(potIdObj);
            await AddFinanceEventAsync("Household Reserve", amount > 0 ? "ContributionAdded" : "ContributionReversed", "ReservePot", potId,
                $"{name.Trim()} {(amount > 0 ? "funded" : "adjusted")}",
                amount > 0 ? "A dashboard household reserve allocation was recorded." : "A previous dashboard allocation was reversed or edited.",
                Math.Abs(amount), "Dashboard");
            await RebuildReservePotFundingHistoryAsync(potId);
        }
    }

    public async Task SyncReservePotContributionAverageAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        await EnsureModernTablesAsync();
        await ExecuteAsync("""
UPDATE dbo.reserve_pots
SET default_monthly_contribution = ISNULL((
        SELECT AVG(month_total)
        FROM (
            SELECT SUM(amount) AS month_total
            FROM dbo.savings
            WHERE [name] = @name AND amount > 0
            GROUP BY YEAR([date]), MONTH([date])
        ) monthly_totals
    ), 0),
    updated_at = SYSUTCDATETIME()
WHERE [name] = @name;
""", ("@name", name.Trim()));
    }

    public async Task SyncAllReservePotContributionAveragesAsync()
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("""
UPDATE p
SET default_monthly_contribution = ISNULL(a.average_monthly_contribution, 0),
    updated_at = SYSUTCDATETIME()
FROM dbo.reserve_pots p
OUTER APPLY (
    SELECT AVG(month_total) AS average_monthly_contribution
    FROM (
        SELECT SUM(s.amount) AS month_total
        FROM dbo.savings s
        WHERE s.[name] = p.[name] AND s.amount > 0
        GROUP BY YEAR(s.[date]), MONTH(s.[date])
    ) monthly_totals
) a;
""");
    }

    public async Task<PaymentRow?> GetPaymentAsync(string source, int id)
    {
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Id: "billid", Date: "[date]", Category: "NULL", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
            "everyday_spending" => (Table: "dbo.everyday_spending", Id: "everyday_spending_id", Date: "[date]", Category: "category", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
            "extra_expenses" => (Table: "dbo.extra_expenses", Id: "extra_expense_id", Date: "duedate", Category: "category", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
            "investments" => (Table: "dbo.investments", Id: "investments_id", Date: "[date]", Category: "category", Type: "NULL", Length: "length", Notes: "notes", IdParam: "@id"),
            "savings" => (Table: "dbo.savings", Id: "savings_id", Date: "[date]", Category: "NULL", Type: "NULL", Length: "length", Notes: "notes", IdParam: "@id"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        var sql = $"SELECT {map.Id} AS id, [name], amount, {map.Date} AS [date], {map.Category} AS category, {map.Type} AS [type], {map.Length} AS [length], {map.Notes} AS notes FROM {map.Table} WHERE {map.Id}=@id";
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync()) return new PaymentRow(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDateTime(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), source);
        return null;
    }

    public async Task UpdatePaymentAsync(string source, int id, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        switch (source)
        {
            case "bills":
                await ExecuteAsync("UPDATE dbo.bills SET [name]=@name, amount=@amount, [date]=@date, [type]=@type, [length]=@length, [description]=@notes WHERE billid=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "everyday_spending":
                await ExecuteAsync("UPDATE dbo.everyday_spending SET [name]=@name, amount=@amount, [date]=@date, category=@category, [type]=@type, [length]=@length, [description]=@notes WHERE everyday_spending_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "extra_expenses":
                await ExecuteAsync("UPDATE dbo.extra_expenses SET [name]=@name, amount=@amount, duedate=@date, category=@category, [type]=@type, [length]=@length, [description]=@notes WHERE extra_expense_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "investments":
                await ExecuteAsync("UPDATE dbo.investments SET [name]=@name, amount=@amount, [date]=@date, category=@category, [length]=@length, notes=@notes WHERE investments_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            case "savings":
                await ExecuteAsync("UPDATE dbo.savings SET [name]=@name, amount=@amount, [date]=@date, [length]=@length, notes=@notes WHERE savings_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@length", DbValue(length)), ("@notes", DbValue(notes)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), "Unknown payment section.");
        }
    }



    public async Task<List<AssetHolding>> GetAssetHoldingsAsync()
    {
        await EnsureModernTablesAsync();
        var list = new List<AssetHolding>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"
SELECT asset_holding_id,[name],asset_type,symbol,quantity,average_buy_price,current_price,current_value,currency,use_live_price,provider,broker,price_source,valuation_method,metal_weight_oz,metal_purity,premium_value,metal_year,bullion_series,bullion_form,manual_value,annual_growth_rate,monthly_contribution,purchase_date,last_price_updated_at,created_at,updated_at
FROM dbo.asset_holdings
ORDER BY CASE asset_type WHEN 'Stock' THEN 1 WHEN 'ETF' THEN 2 WHEN 'Crypto' THEN 3 WHEN 'Gold' THEN 4 WHEN 'Silver' THEN 5 WHEN 'Cash' THEN 6 ELSE 9 END, [name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new AssetHolding
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                AssetType = r.GetString(2),
                Symbol = r.IsDBNull(3) ? null : r.GetString(3),
                Quantity = r.GetDecimal(4),
                AverageBuyPrice = r.IsDBNull(5) ? null : r.GetDecimal(5),
                CurrentPrice = r.IsDBNull(6) ? null : r.GetDecimal(6),
                CurrentValue = r.IsDBNull(7) ? null : r.GetDecimal(7),
                Currency = r.GetString(8),
                UseLivePrice = r.GetBoolean(9),
                Provider = r.IsDBNull(10) ? null : r.GetString(10),
                Broker = r.IsDBNull(11) ? null : r.GetString(11),
                PriceSource = r.IsDBNull(12) ? "Auto" : r.GetString(12),
                ValuationMethod = r.IsDBNull(13) ? "SpotPremium" : r.GetString(13),
                MetalWeightOz = r.IsDBNull(14) ? null : r.GetDecimal(14),
                MetalPurity = r.IsDBNull(15) ? null : r.GetDecimal(15),
                PremiumValue = r.IsDBNull(16) ? null : r.GetDecimal(16),
                MetalYear = r.IsDBNull(17) ? null : r.GetInt32(17),
                BullionSeries = r.IsDBNull(18) ? null : r.GetString(18),
                BullionForm = r.IsDBNull(19) ? null : r.GetString(19),
                ManualValue = r.IsDBNull(20) ? null : r.GetDecimal(20),
                AnnualGrowthRate = r.IsDBNull(21) ? null : r.GetDecimal(21),
                MonthlyContribution = r.IsDBNull(22) ? null : r.GetDecimal(22),
                PurchaseDate = r.IsDBNull(23) ? null : r.GetDateTime(23),
                LastPriceUpdatedAt = r.IsDBNull(24) ? null : r.GetDateTime(24),
                CreatedAt = r.GetDateTime(25),
                UpdatedAt = r.GetDateTime(26)
            });
        }
        return list;
    }

    public async Task<AssetHolding?> GetAssetHoldingAsync(int id)
    {
        return (await GetAssetHoldingsAsync()).FirstOrDefault(a => a.Id == id);
    }

    public async Task<AssetSummary> GetAssetSummaryAsync()
    {
        var assets = await GetAssetHoldingsAsync();
        var total = assets.Sum(a => a.DisplayValue);
        var monthly = assets.Sum(a => a.MonthlyContribution ?? 0m);
        var rate = total <= 0 ? 0m : assets.Sum(a => a.DisplayValue * (a.AnnualGrowthRate ?? 0m)) / total;
        var last = assets.Where(a => a.LastPriceUpdatedAt.HasValue || a.UpdatedAt > DateTime.MinValue)
            .Select(a => a.LastPriceUpdatedAt ?? a.UpdatedAt)
            .OrderByDescending(x => x)
            .FirstOrDefault();
        return new AssetSummary(Math.Round(total, 2), Math.Round(monthly, 2), Math.Round(rate, 4), last == default ? null : last);
    }

    public async Task SaveAssetHoldingAsync(AssetHolding asset)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(asset.Name)) throw new ArgumentException("Asset name is required.", nameof(asset));
        asset.Broker = string.IsNullOrWhiteSpace(asset.Broker) ? asset.Provider : asset.Broker.Trim();
        asset.Provider = asset.Broker;
        asset.PriceSource = string.IsNullOrWhiteSpace(asset.PriceSource) ? "Auto" : asset.PriceSource.Trim();
        asset.Symbol = string.IsNullOrWhiteSpace(asset.Symbol) ? null : asset.Symbol.Trim().ToUpperInvariant();

        if (asset.AssetType.Equals("Gold", StringComparison.OrdinalIgnoreCase) || asset.AssetType.Equals("Silver", StringComparison.OrdinalIgnoreCase))
        {
            // Bullion does not need a ticker in the UI. Internally keep XAU/XAG so older rows still make sense.
            asset.Symbol = asset.AssetType.Equals("Gold", StringComparison.OrdinalIgnoreCase) ? "XAU" : "XAG";
            asset.MetalPurity ??= asset.AssetType.Equals("Gold", StringComparison.OrdinalIgnoreCase) ? 0.9999m : 0.999m;
            asset.BullionForm = string.IsNullOrWhiteSpace(asset.BullionForm) ? "Bullion Coin" : asset.BullionForm.Trim();
            asset.BullionSeries = string.IsNullOrWhiteSpace(asset.BullionSeries) ? null : asset.BullionSeries.Trim();
            asset.ValuationMethod = string.IsNullOrWhiteSpace(asset.ValuationMethod) ?
                (asset.BullionForm is "Proof Coin" or "Commemorative Coin" or "Coin Set" or "Medal" ? "Manual" : "SpotPremium")
                : asset.ValuationMethod.Trim();
        }

        var isBullion = asset.AssetType.Equals("Gold", StringComparison.OrdinalIgnoreCase) || asset.AssetType.Equals("Silver", StringComparison.OrdinalIgnoreCase);
        var usesSpotValuation = isBullion && (asset.ValuationMethod.Equals("SpotPremium", StringComparison.OrdinalIgnoreCase) || asset.ValuationMethod.Equals("Spot", StringComparison.OrdinalIgnoreCase));
        if (isBullion && !usesSpotValuation && !asset.ManualValue.HasValue)
        {
            asset.ManualValue = Math.Round(asset.Quantity * (asset.AverageBuyPrice ?? 0m), 2);
        }

        var currentValue = isBullion && !usesSpotValuation
            ? asset.ManualValue ?? asset.CurrentValue ?? Math.Round(asset.Quantity * (asset.AverageBuyPrice ?? 0m), 2)
            : asset.UseLivePrice && asset.CurrentPrice.HasValue
                ? (isBullion
                    ? Math.Round(asset.Quantity * (asset.MetalWeightOz ?? 1m) * asset.CurrentPrice.Value + (asset.PremiumValue ?? 0m), 2)
                    : Math.Round(asset.Quantity * asset.CurrentPrice.Value, 2))
                : asset.ManualValue ?? asset.CurrentValue ?? (isBullion
                    ? Math.Round(asset.Quantity * (asset.MetalWeightOz ?? 1m) * (asset.CurrentPrice ?? 0m) + (asset.PremiumValue ?? 0m), 2)
                    : Math.Round(asset.Quantity * (asset.CurrentPrice ?? asset.AverageBuyPrice ?? 0m), 2));

        if (asset.Id == 0)
        {
            await ExecuteAsync(@"
INSERT INTO dbo.asset_holdings([name],asset_type,symbol,quantity,average_buy_price,current_price,current_value,currency,use_live_price,provider,broker,price_source,valuation_method,metal_weight_oz,metal_purity,premium_value,metal_year,bullion_series,bullion_form,manual_value,annual_growth_rate,monthly_contribution,purchase_date,last_price_updated_at)
VALUES(@name,@type,@symbol,@quantity,@avg,@price,@value,@currency,@live,@provider,@broker,@priceSource,@valuationMethod,@metalWeight,@purity,@premium,@metalYear,@series,@form,@manual,@growth,@monthly,@purchaseDate,@last)",
                ("@name", asset.Name.Trim()), ("@type", asset.AssetType), ("@symbol", DbValue(asset.Symbol)), ("@quantity", asset.Quantity),
                ("@avg", asset.AverageBuyPrice.HasValue ? asset.AverageBuyPrice.Value : (object)DBNull.Value), ("@price", asset.CurrentPrice.HasValue ? asset.CurrentPrice.Value : (object)DBNull.Value),
                ("@value", currentValue), ("@currency", string.IsNullOrWhiteSpace(asset.Currency) ? "GBP" : asset.Currency.Trim().ToUpperInvariant()),
                ("@live", asset.UseLivePrice), ("@provider", DbValue(asset.Broker)), ("@broker", DbValue(asset.Broker)), ("@priceSource", asset.PriceSource), ("@valuationMethod", asset.ValuationMethod),
                ("@metalWeight", asset.MetalWeightOz.HasValue ? asset.MetalWeightOz.Value : (object)DBNull.Value), ("@purity", asset.MetalPurity.HasValue ? asset.MetalPurity.Value : (object)DBNull.Value),
                ("@premium", asset.PremiumValue.HasValue ? asset.PremiumValue.Value : (object)DBNull.Value), ("@metalYear", asset.MetalYear.HasValue ? asset.MetalYear.Value : (object)DBNull.Value),
                ("@series", DbValue(asset.BullionSeries)), ("@form", DbValue(asset.BullionForm)), ("@manual", asset.ManualValue.HasValue ? asset.ManualValue.Value : (object)DBNull.Value),
                ("@growth", asset.AnnualGrowthRate.HasValue ? asset.AnnualGrowthRate.Value : (object)DBNull.Value), ("@monthly", asset.MonthlyContribution.HasValue ? asset.MonthlyContribution.Value : (object)DBNull.Value),
                ("@purchaseDate", asset.PurchaseDate.HasValue ? asset.PurchaseDate.Value.Date : (object)DBNull.Value),
                ("@last", asset.LastPriceUpdatedAt.HasValue ? asset.LastPriceUpdatedAt.Value : (object)DBNull.Value));
        }
        else
        {
            await ExecuteAsync(@"
UPDATE dbo.asset_holdings
SET [name]=@name, asset_type=@type, symbol=@symbol, quantity=@quantity, average_buy_price=@avg, current_price=@price, current_value=@value, currency=@currency, use_live_price=@live, provider=@provider, broker=@broker, price_source=@priceSource, valuation_method=@valuationMethod, metal_weight_oz=@metalWeight, metal_purity=@purity, premium_value=@premium, metal_year=@metalYear, bullion_series=@series, bullion_form=@form, manual_value=@manual, annual_growth_rate=@growth, monthly_contribution=@monthly, purchase_date=@purchaseDate, last_price_updated_at=@last, updated_at=SYSUTCDATETIME()
WHERE asset_holding_id=@id",
                ("@id", asset.Id), ("@name", asset.Name.Trim()), ("@type", asset.AssetType), ("@symbol", DbValue(asset.Symbol)), ("@quantity", asset.Quantity),
                ("@avg", asset.AverageBuyPrice.HasValue ? asset.AverageBuyPrice.Value : (object)DBNull.Value), ("@price", asset.CurrentPrice.HasValue ? asset.CurrentPrice.Value : (object)DBNull.Value),
                ("@value", currentValue), ("@currency", string.IsNullOrWhiteSpace(asset.Currency) ? "GBP" : asset.Currency.Trim().ToUpperInvariant()),
                ("@live", asset.UseLivePrice), ("@provider", DbValue(asset.Broker)), ("@broker", DbValue(asset.Broker)), ("@priceSource", asset.PriceSource), ("@valuationMethod", asset.ValuationMethod),
                ("@metalWeight", asset.MetalWeightOz.HasValue ? asset.MetalWeightOz.Value : (object)DBNull.Value), ("@purity", asset.MetalPurity.HasValue ? asset.MetalPurity.Value : (object)DBNull.Value),
                ("@premium", asset.PremiumValue.HasValue ? asset.PremiumValue.Value : (object)DBNull.Value), ("@metalYear", asset.MetalYear.HasValue ? asset.MetalYear.Value : (object)DBNull.Value),
                ("@series", DbValue(asset.BullionSeries)), ("@form", DbValue(asset.BullionForm)), ("@manual", asset.ManualValue.HasValue ? asset.ManualValue.Value : (object)DBNull.Value),
                ("@growth", asset.AnnualGrowthRate.HasValue ? asset.AnnualGrowthRate.Value : (object)DBNull.Value), ("@monthly", asset.MonthlyContribution.HasValue ? asset.MonthlyContribution.Value : (object)DBNull.Value),
                ("@purchaseDate", asset.PurchaseDate.HasValue ? asset.PurchaseDate.Value.Date : (object)DBNull.Value),
                ("@last", asset.LastPriceUpdatedAt.HasValue ? asset.LastPriceUpdatedAt.Value : (object)DBNull.Value));
        }
    }

    public async Task DeleteAssetHoldingAsync(int id)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("DELETE FROM dbo.asset_holdings WHERE asset_holding_id=@id", ("@id", id));
    }

    public async Task UpdateAssetLivePriceAsync(int id, decimal currentPrice, DateTime updatedAt)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"
UPDATE dbo.asset_holdings
SET current_price=@price,
    current_value=CASE
        WHEN asset_type IN ('Gold','Silver') AND ISNULL(valuation_method,'SpotPremium') IN ('SpotPremium','Spot') THEN ROUND(quantity * ISNULL(metal_weight_oz, 1) * @price + ISNULL(premium_value, 0), 2)
        WHEN asset_type IN ('Gold','Silver') THEN ISNULL(manual_value, current_value)
        ELSE ROUND(quantity * @price, 2)
    END,
    last_price_updated_at=@updated,
    updated_at=SYSUTCDATETIME()
WHERE asset_holding_id=@id", ("@id", id), ("@price", currentPrice), ("@updated", updatedAt));
    }

    public async Task<string?> GetLoginPasswordHashAsync()
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync("SELECT TOP 1 password_hash FROM dbo.app_login WHERE app_login_id = 1");
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    public async Task SaveLoginPasswordHashAsync(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"
MERGE dbo.app_login AS t
USING (SELECT 1 AS app_login_id) AS s
ON t.app_login_id = s.app_login_id
WHEN MATCHED THEN
    UPDATE SET password_hash = @hash, updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT(app_login_id, password_hash) VALUES(1, @hash);", ("@hash", passwordHash));
    }

    public async Task<decimal> GetDecimalSettingAsync(string key, decimal fallback) { var db = await ScalarAsync("IF OBJECT_ID('dbo.finance_settings','U') IS NOT NULL SELECT [value] FROM dbo.finance_settings WHERE [key]=@key", ("@key", key)); return decimal.TryParse(Convert.ToString(db), out var v) ? v : (decimal.TryParse(config[$"FinanceSettings:{key}"], out var c) ? c : fallback); }
    public async Task SaveDecimalSettingAsync(string key, decimal value) { await EnsureModernTablesAsync(); await ExecuteAsync("MERGE dbo.finance_settings AS t USING (SELECT @key AS [key]) AS s ON t.[key]=s.[key] WHEN MATCHED THEN UPDATE SET [value]=@value, updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([key],[value]) VALUES(@key,@value);", ("@key", key), ("@value", value)); }
    private async Task<object?> ScalarAsync(string sql, params (string, object)[] ps) { await using var con = new SqlConnection(ConnStr); await con.OpenAsync(); await using var cmd = new SqlCommand(sql, con); foreach (var p in ps) cmd.Parameters.AddWithValue(p.Item1, p.Item2); return await cmd.ExecuteScalarAsync(); }
    private async Task ExecuteAsync(string sql, params (string, object)[] ps) { await using var con = new SqlConnection(ConnStr); await con.OpenAsync(); await using var cmd = new SqlCommand(sql, con); foreach (var p in ps) cmd.Parameters.AddWithValue(p.Item1, p.Item2); await cmd.ExecuteNonQueryAsync(); }

    public async Task<List<SavingPot>> GetSavingPotsAsync()
    {
        await EnsureModernTablesAsync();
        var list = new List<SavingPot>();
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT saving_pot_id,[name],target_amount,monthly_amount,
       ISNULL(contribution_mode,'Uses global savings schedule'),
       ISNULL([priority],1),ISNULL(pot_type,'Goal'),ISNULL(access_speed,'Immediate'),ISNULL(interest_rate,0),target_date,destination,
       created_at,updated_at
FROM dbo.saving_pots
ORDER BY [priority], CASE WHEN target_date IS NULL THEN 1 ELSE 0 END, target_date, created_at", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SavingPot(
                r.GetInt32(0),
                r.GetString(1),
                r.GetDecimal(2),
                r.GetDecimal(3),
                r.GetString(4),
                r.GetInt32(5),
                r.GetString(6),
                r.GetString(7),
                r.GetDecimal(8),
                r.IsDBNull(9) ? null : r.GetDateTime(9),
                r.IsDBNull(10) ? null : r.GetString(10),
                r.GetDateTime(11),
                r.GetDateTime(12)));
        }
        return list;
    }

    public async Task<List<SavingPotMonth>> GetSavingPotMonthsAsync(int year)
    {
        await EnsureModernTablesAsync();

        var list = new List<SavingPotMonth>();

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();

        await using var cmd = new SqlCommand(@"
SELECT saving_pot_month_id,
       saving_pot_id,
       [year],
       [month],
       is_saved,
       saved_amount,
       updated_at
FROM dbo.saving_pot_months
WHERE [year] = @year
ORDER BY saving_pot_id, [month]", con);

        cmd.Parameters.AddWithValue("@year", year);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new SavingPotMonth(
                r.GetInt32(0),
                r.GetInt32(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetBoolean(4),
                r.GetDecimal(5),
                r.GetDateTime(6)
            ));
        }

        return list;
    }

    public async Task<decimal> GetTotalAllocatedToSavingPotsAsync()
    {
        await EnsureModernTablesAsync();

        var value = await ScalarAsync(@"
SELECT
    COALESCE((SELECT SUM(saved_amount)
              FROM dbo.saving_pot_months
              WHERE is_saved = 1), 0)
    +
    COALESCE((SELECT SUM(amount)
              FROM dbo.saving_pot_extras), 0)");

        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    public async Task SaveSavingPotAsync(int id, string name, decimal targetAmount, decimal monthlyAmount, string contributionMode = "Uses global savings schedule", int priority = 1, string potType = "Goal", string accessSpeed = "Immediate", decimal interestRate = 0m, DateTime? targetDate = null, string? destination = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pot name is required.", nameof(name));
        await EnsureModernTablesAsync();
        priority = Math.Max(1, priority);
        potType = string.IsNullOrWhiteSpace(potType) ? "Goal" : potType.Trim();
        accessSpeed = string.IsNullOrWhiteSpace(accessSpeed) ? "Immediate" : accessSpeed.Trim();
        var allowedContributionModes = new[] { "Uses global savings schedule", "Manual contribution", "One-off funding only", "Paused" };
        contributionMode = allowedContributionModes.Contains(contributionMode?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? allowedContributionModes.First(x => x.Equals(contributionMode!.Trim(), StringComparison.OrdinalIgnoreCase))
            : "Uses global savings schedule";
        if (!contributionMode.Equals("Manual contribution", StringComparison.OrdinalIgnoreCase)) monthlyAmount = 0m;
        if (id == 0)
        {
            await ExecuteAsync(@"INSERT INTO dbo.saving_pots([name],target_amount,monthly_amount,contribution_mode,[priority],pot_type,access_speed,interest_rate,target_date,destination)
VALUES(@name,@target,@monthly,@contributionMode,@priority,@potType,@accessSpeed,@interestRate,@targetDate,@destination)",
                ("@name", name.Trim()), ("@target", targetAmount), ("@monthly", monthlyAmount), ("@contributionMode", contributionMode), ("@priority", priority), ("@potType", potType), ("@accessSpeed", accessSpeed), ("@interestRate", interestRate), ("@targetDate", targetDate.HasValue ? targetDate.Value.Date : (object)DBNull.Value), ("@destination", DbValue(destination)));
        }
        else
        {
            await ExecuteAsync(@"UPDATE dbo.saving_pots
SET [name]=@name,target_amount=@target,monthly_amount=@monthly,contribution_mode=@contributionMode,[priority]=@priority,pot_type=@potType,access_speed=@accessSpeed,interest_rate=@interestRate,target_date=@targetDate,destination=@destination,updated_at=SYSUTCDATETIME()
WHERE saving_pot_id=@id", ("@id", id), ("@name", name.Trim()), ("@target", targetAmount), ("@monthly", monthlyAmount), ("@contributionMode", contributionMode), ("@priority", priority), ("@potType", potType), ("@accessSpeed", accessSpeed), ("@interestRate", interestRate), ("@targetDate", targetDate.HasValue ? targetDate.Value.Date : (object)DBNull.Value), ("@destination", DbValue(destination)));
        }
    }


    public async Task<List<ReservedFund>> GetReservedFundsAsync()
    {
        await EnsureModernTablesAsync();
        var list = new List<ReservedFund>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT reserved_fund_id,[name],amount,category,access_speed,include_in_net_worth,deduct_from_savings_allocation,notes,created_at,updated_at FROM dbo.reserved_funds ORDER BY category,[name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ReservedFund(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetString(3), r.GetString(4), r.GetBoolean(5), r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetDateTime(8), r.GetDateTime(9)));
        }
        return list;
    }

    public async Task SaveReservedFundAsync(int id, string name, decimal amount, string category, string accessSpeed, bool includeInNetWorth, bool deductFromSavingsAllocation, string? notes)
    {
        await EnsureModernTablesAsync();
        name = string.IsNullOrWhiteSpace(name) ? "Reserved fund" : name.Trim();
        category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim();
        accessSpeed = string.IsNullOrWhiteSpace(accessSpeed) ? "Within 1 week" : accessSpeed.Trim();
        if (id == 0)
        {
            await ExecuteAsync(@"INSERT INTO dbo.reserved_funds([name],amount,category,access_speed,include_in_net_worth,deduct_from_savings_allocation,notes)
VALUES(@name,@amount,@category,@access,@networth,@deduct,@notes)", ("@name", name), ("@amount", Math.Max(0, amount)), ("@category", category), ("@access", accessSpeed), ("@networth", includeInNetWorth), ("@deduct", deductFromSavingsAllocation), ("@notes", DbValue(notes)));
        }
        else
        {
            await ExecuteAsync(@"UPDATE dbo.reserved_funds SET [name]=@name,amount=@amount,category=@category,access_speed=@access,include_in_net_worth=@networth,deduct_from_savings_allocation=@deduct,notes=@notes,updated_at=SYSUTCDATETIME() WHERE reserved_fund_id=@id", ("@id", id), ("@name", name), ("@amount", Math.Max(0, amount)), ("@category", category), ("@access", accessSpeed), ("@networth", includeInNetWorth), ("@deduct", deductFromSavingsAllocation), ("@notes", DbValue(notes)));
        }
    }

    public async Task DeleteReservedFundAsync(int id)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("DELETE FROM dbo.reserved_funds WHERE reserved_fund_id=@id", ("@id", id));
    }

    public async Task SaveSavingsBrainSettingsAsync(decimal emergencyBaseline, decimal monthlySavingRate, DateTime overallTargetDate)
    {
        await SaveDecimalSettingAsync("EmergencyFundBaseline", emergencyBaseline);
        await SaveDecimalSettingAsync("SavingsMonthlyRate", monthlySavingRate);
        await SaveDecimalSettingAsync("SavingsOverallTargetYear", overallTargetDate.Year);
        await SaveDecimalSettingAsync("SavingsOverallTargetMonth", overallTargetDate.Month);
    }

    public async Task DeleteSavingPotAsync(int id)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("DELETE FROM dbo.saving_pots WHERE saving_pot_id=@id", ("@id", id));
    }

    public async Task ToggleSavingPotMonthAsync(int potId, int year, int month)
    {
        await EnsureModernTablesAsync();

        await ExecuteAsync(@"
MERGE dbo.saving_pot_months AS t
USING (
    SELECT 
        @potId AS pot_id,
        @year AS y,
        @month AS m,
        monthly_amount
    FROM dbo.saving_pots
    WHERE saving_pot_id = @potId
) AS s
ON t.saving_pot_id = s.pot_id
AND t.[year] = s.y
AND t.[month] = s.m
WHEN MATCHED THEN
    UPDATE SET
        is_saved = CASE WHEN t.is_saved = 1 THEN 0 ELSE 1 END,
        saved_amount = CASE WHEN t.is_saved = 1 THEN 0 ELSE s.monthly_amount END,
        updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT(saving_pot_id,[year],[month],is_saved,saved_amount)
    VALUES(@potId,@year,@month,1,s.monthly_amount);",
        ("@potId", potId),
        ("@year", year),
        ("@month", month));
    }

    public async Task AddSavingPotExtraAsync(int potId, decimal amount, DateTime date, string? note)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

        await EnsureModernTablesAsync();

        await ExecuteAsync(@"
INSERT INTO dbo.saving_pot_extras(saving_pot_id, amount, [date], note)
VALUES(@potId, @amount, @date, @note)",
        ("@potId", potId),
        ("@amount", amount),
        ("@date", date.Date),
        ("@note", DbValue(note)));
    }

    public async Task<IReadOnlySet<int>> GetSelectedReserveAccountIdsAsync()
    {
        await EnsureModernTablesAsync();
        var ids = new HashSet<int>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT account_id FROM dbo.reserve_account_selections ORDER BY display_order, account_id", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        return ids;
    }

    public async Task SaveReserveAccountSelectionAsync(IEnumerable<int> accountIds)
    {
        await EnsureModernTablesAsync();
        var ids = accountIds.Distinct().Where(x => x >= 0).ToArray();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = await con.BeginTransactionAsync();
        try
        {
            await using (var delete = new SqlCommand("DELETE FROM dbo.reserve_account_selections", con, (SqlTransaction)tx))
                await delete.ExecuteNonQueryAsync();

            for (var i = 0; i < ids.Length; i++)
            {
                await using var insert = new SqlCommand("INSERT INTO dbo.reserve_account_selections(account_id, display_order) VALUES(@id,@order)", con, (SqlTransaction)tx);
                insert.Parameters.AddWithValue("@id", ids[i]);
                insert.Parameters.AddWithValue("@order", i);
                await insert.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<HouseholdReserve> GetHouseholdReserveAsync()
    {
        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT balance,interest_rate,provider,updated_at FROM dbo.household_reserve WHERE household_reserve_id=1", con);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync()
            ? new HouseholdReserve(r.GetDecimal(0), r.GetDecimal(1), r.GetString(2), r.GetDateTime(3))
            : new HouseholdReserve(0, 0, "Money market fund", DateTime.MinValue);
    }

    public async Task SaveHouseholdReserveAsync(decimal balance, decimal interestRate, string? provider)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"MERGE dbo.household_reserve AS t USING (SELECT 1 AS id) AS s ON t.household_reserve_id=s.id
WHEN MATCHED THEN UPDATE SET balance=@balance, interest_rate=@rate, provider=@provider, updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(household_reserve_id,balance,interest_rate,provider) VALUES(1,@balance,@rate,@provider);",
            ("@balance", Math.Max(0,balance)), ("@rate", Math.Max(0,interestRate)), ("@provider", string.IsNullOrWhiteSpace(provider) ? "Money market fund" : provider.Trim()));
    }

    public async Task<List<ReservePot>> GetReservePotsAsync()
    {
        await EnsureModernTablesAsync();
        var list = new List<ReservePot>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT reserve_pot_id,[name],allocated_amount,default_monthly_contribution,intended_monthly_contribution,funding_frequency,expected_funding_day,carry_forward_shortfalls,carry_excess_forward,funding_paused_from,funding_paused_until,funding_pause_reason,funding_plan_start_date,target_amount,due_date,priority,is_active,notes,updated_at FROM dbo.reserve_pots ORDER BY priority,[name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ReservePot(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetInt32(6), r.GetBoolean(7), r.GetBoolean(8), r.IsDBNull(9) ? null : r.GetDateTime(9), r.IsDBNull(10) ? null : r.GetDateTime(10), r.IsDBNull(11) ? null : r.GetString(11), r.GetDateTime(12), r.IsDBNull(13) ? null : r.GetDecimal(13), r.IsDBNull(14) ? null : r.GetDateTime(14), r.GetInt32(15), r.GetBoolean(16), r.IsDBNull(17) ? null : r.GetString(17), r.GetDateTime(18)));
        return list;
    }

    public async Task<int> SaveReservePotAsync(int id, string name, decimal allocatedAmount, decimal monthlyContribution, decimal intendedMonthlyContribution, string fundingFrequency, int? expectedFundingDay, bool carryForwardShortfalls, bool carryExcessForward, DateTime? fundingPausedFrom, DateTime? fundingPausedUntil, string? fundingPauseReason, DateTime? fundingPlanStartDate, decimal? targetAmount, DateTime? dueDate, int priority, bool isActive, string? notes)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pot name is required.", nameof(name));

        var allowedFrequencies = new[] { "Monthly", "Weekly", "Irregular" };
        var normalisedFrequency = allowedFrequencies.Contains(fundingFrequency, StringComparer.OrdinalIgnoreCase)
            ? allowedFrequencies.First(x => x.Equals(fundingFrequency, StringComparison.OrdinalIgnoreCase))
            : "Monthly";

        if (expectedFundingDay is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(expectedFundingDay), "Expected funding day must be between 1 and 31.");

        if (fundingPausedUntil.HasValue && !fundingPausedFrom.HasValue)
            fundingPausedFrom = DateTime.Today;
        if (fundingPausedFrom.HasValue && !fundingPausedUntil.HasValue)
            throw new ArgumentException("Choose a pause-until date when setting a pause start date.", nameof(fundingPausedUntil));
        if (fundingPausedFrom.HasValue && fundingPausedUntil.HasValue && fundingPausedUntil.Value.Date < fundingPausedFrom.Value.Date)
            throw new ArgumentException("Pause-until date cannot be before the pause start date.", nameof(fundingPausedUntil));

        var parameters = new (string, object)[]
        {
            ("@name", name.Trim()),
            ("@allocated", Math.Max(0, allocatedAmount)),
            ("@monthly", Math.Max(0, monthlyContribution)),
            ("@intended", Math.Max(0, intendedMonthlyContribution)),
            ("@frequency", normalisedFrequency),
            ("@fundingDay", expectedFundingDay.HasValue ? expectedFundingDay.Value : DBNull.Value),
            ("@carryForward", carryForwardShortfalls),
            ("@carryExcess", carryExcessForward),
            ("@pausedFrom", fundingPausedFrom.HasValue ? fundingPausedFrom.Value.Date : DBNull.Value),
            ("@pausedUntil", fundingPausedUntil.HasValue ? fundingPausedUntil.Value.Date : DBNull.Value),
            ("@pauseReason", DbValue(fundingPauseReason)),
            ("@planStart", (fundingPlanStartDate ?? DateTime.Today).Date),
            ("@target", targetAmount.HasValue ? Math.Max(0, targetAmount.Value) : DBNull.Value),
            ("@due", dueDate.HasValue ? dueDate.Value.Date : DBNull.Value),
            ("@priority", Math.Max(1, priority)),
            ("@active", isActive),
            ("@notes", DbValue(notes))
        };

        if (id <= 0)
        {
            await ExecuteAsync(@"INSERT INTO dbo.reserve_pots([name],allocated_amount,default_monthly_contribution,intended_monthly_contribution,funding_frequency,expected_funding_day,carry_forward_shortfalls,carry_excess_forward,funding_paused_from,funding_paused_until,funding_pause_reason,funding_plan_start_date,target_amount,due_date,priority,is_active,notes)
VALUES(@name,@allocated,@monthly,@intended,@frequency,@fundingDay,@carryForward,@carryExcess,@pausedFrom,@pausedUntil,@pauseReason,@planStart,@target,@due,@priority,@active,@notes)", parameters);
            id = Convert.ToInt32(await ScalarAsync("SELECT TOP 1 reserve_pot_id FROM dbo.reserve_pots WHERE [name]=@name ORDER BY reserve_pot_id DESC", ("@name", name.Trim())));
            await AddFinanceEventAsync("Household Reserve", "PotCreated", "ReservePot", id, $"{name.Trim()} created", "A new virtual allocation was created.", allocatedAmount, "User");
        }
        else
        {
            await ExecuteAsync(@"UPDATE dbo.reserve_pots SET [name]=@name,allocated_amount=@allocated,default_monthly_contribution=@monthly,intended_monthly_contribution=@intended,funding_frequency=@frequency,expected_funding_day=@fundingDay,carry_forward_shortfalls=@carryForward,carry_excess_forward=@carryExcess,funding_paused_from=@pausedFrom,funding_paused_until=@pausedUntil,funding_pause_reason=@pauseReason,funding_plan_start_date=@planStart,target_amount=@target,due_date=@due,priority=@priority,is_active=@active,notes=@notes,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id", parameters.Append(("@id", (object)id)).ToArray());
            await AddFinanceEventAsync("Household Reserve", "PotUpdated", "ReservePot", id, $"{name.Trim()} updated", "Funding settings or allocation details were changed.", allocatedAmount, "User");
        }

        await RebuildReservePotFundingHistoryAsync(id);
        return id;
    }

    public async Task<Dictionary<int, ReservePotFundingSummary>> GetReservePotFundingSummariesAsync(IReadOnlyList<ReservePot>? pots = null)
    {
        await EnsureModernTablesAsync();
        pots ??= await GetReservePotsAsync();
        var result = new Dictionary<int, ReservePotFundingSummary>();
        foreach (var pot in pots)
        {
            await RebuildReservePotFundingHistoryAsync(pot.Id);
            result[pot.Id] = await GetReservePotFundingSummaryAsync(pot);
        }
        return result;
    }

    public async Task<DateTime?> GetFirstReservePotContributionDateAsync(string potName)
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync("SELECT MIN(CAST([date] AS date)) FROM dbo.savings WHERE [name]=@name AND amount>0", ("@name", potName));
        return value is null || value == DBNull.Value ? null : Convert.ToDateTime(value);
    }

    public async Task RebuildReservePotFundingHistoryAsync(int potId, DateTime? overrideStartDate = null)
    {
        await EnsureModernTablesAsync();
        var pot = (await GetReservePotsAsync()).FirstOrDefault(x => x.Id == potId);
        if (pot is null) return;

        var chosenStart = overrideStartDate ?? pot.FundingPlanStartDate;
        var start = new DateTime(chosenStart.Year, chosenStart.Month, 1);
        var current = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (start > current) start = current;

        if (overrideStartDate.HasValue)
            await ExecuteAsync("UPDATE dbo.reserve_pots SET funding_plan_start_date=@start,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id", ("@start", overrideStartDate.Value.Date), ("@id", potId));

        var oldStatuses = new Dictionary<(int Year, int Month), string>();
        await using (var con = new SqlConnection(ConnStr))
        {
            await con.OpenAsync();
            await using var cmd = new SqlCommand("SELECT [year],[month],[status] FROM dbo.reserve_pot_monthly_funding WHERE reserve_pot_id=@id", con);
            cmd.Parameters.AddWithValue("@id", potId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) oldStatuses[(reader.GetInt32(0), reader.GetInt32(1))] = reader.GetString(2);
        }

        decimal outstandingRecovery = 0m;
        decimal carriedExcess = 0m;
        var recoveryQueue = new LinkedList<(int Year, int Month, decimal Remaining)>();

        // Recovery allocations are rebuilt deterministically with the monthly history.
        await ExecuteAsync("DELETE FROM dbo.reserve_pot_recovery_allocations WHERE reserve_pot_id=@id", ("@id", potId));

        for (var month = start; month <= current; month = month.AddMonths(1))
        {
            var next = month.AddMonths(1);
            var grossContributions = Convert.ToDecimal(await ScalarAsync(
                "SELECT ISNULL(SUM(amount),0) FROM dbo.savings WHERE [name]=@name AND [date]>=@start AND [date]<@end AND amount>0",
                ("@name", pot.Name), ("@start", month), ("@end", next)) ?? 0m);

            var withdrawals = Convert.ToDecimal(await ScalarAsync(
                @"SELECT ISNULL(SUM(amount),0)
FROM dbo.reserve_pot_actions
WHERE reserve_pot_id=@id
  AND action_type='Withdrawal'
  AND action_date>=@start
  AND action_date<@end",
                ("@id", potId), ("@start", month), ("@end", next)) ?? 0m);

            // Withdrawals first reverse funding recorded in the same month. Any remaining
            // withdrawal then consumes carried excess from earlier months. A withdrawal
            // must never appear as new funding or create additional available credit.
            var actual = Math.Max(0m, grossContributions - withdrawals);
            var withdrawalRemaining = Math.Max(0m, withdrawals - grossContributions);
            carriedExcess = Math.Max(0m, carriedExcess - withdrawalRemaining);

            var calculation = fundingEngine.Calculate(new FundingEngineInput(
                Pot: pot,
                PeriodStart: month,
                AsOfDate: DateTime.Today,
                DashboardContribution: actual,
                OutstandingRecovery: outstandingRecovery,
                CarriedExcessBalance: carriedExcess,
                IsCurrentPeriod: month == current));

            var paused = calculation.IsPaused;
            var expected = calculation.ExpectedAmount;
            var appliedToCurrent = calculation.AppliedToCurrentMonth;
            var appliedToRecovery = calculation.AppliedToRecovery;
            var carriedExcessUsed = calculation.CarriedExcessUsed;
            var carriedExcessCreated = calculation.CarriedExcessCreated;
            var genuineExcess = calculation.GenuineExcess;
            var shortfall = calculation.ShortfallAmount;
            var status = calculation.Status;
            outstandingRecovery = calculation.RecoveryBalance;
            carriedExcess = calculation.CarriedExcessBalance;

            // Apply recovery to the oldest outstanding shortfall first and preserve the link.
            var recoveryToAllocate = appliedToRecovery;
            while (recoveryToAllocate > 0m && recoveryQueue.First is not null)
            {
                var debt = recoveryQueue.First.Value;
                recoveryQueue.RemoveFirst();
                var allocated = Math.Min(recoveryToAllocate, debt.Remaining);
                await ExecuteAsync(@"INSERT INTO dbo.reserve_pot_recovery_allocations
(reserve_pot_id,source_year,source_month,target_year,target_month,amount)
VALUES(@potId,@sourceYear,@sourceMonth,@targetYear,@targetMonth,@amount)",
                    ("@potId", potId), ("@sourceYear", month.Year), ("@sourceMonth", month.Month),
                    ("@targetYear", debt.Year), ("@targetMonth", debt.Month), ("@amount", allocated));
                recoveryToAllocate -= allocated;
                var remainingDebt = debt.Remaining - allocated;
                if (remainingDebt > 0m)
                    recoveryQueue.AddFirst((debt.Year, debt.Month, remainingDebt));
            }

            if (shortfall > 0m && pot.CarryForwardShortfalls)
                recoveryQueue.AddLast((month.Year, month.Month, shortfall));

            await ExecuteAsync(@"MERGE dbo.reserve_pot_monthly_funding AS target
USING (SELECT @potId reserve_pot_id,@year [year],@month [month]) source
ON target.reserve_pot_id=source.reserve_pot_id AND target.[year]=source.[year] AND target.[month]=source.[month]
WHEN MATCHED THEN UPDATE SET expected_amount=@expected,actual_amount=@actual,
    applied_to_current_month=@currentApplied,applied_to_recovery=@recoveryApplied,
    carried_excess_used=@creditUsed,carried_excess_created=@creditCreated,
    shortfall_amount=@shortfall,genuine_excess=@genuineExcess,
    recovery_balance=@recoveryBalance,carried_excess_balance=@creditBalance,
    [status]=@status,is_paused=@paused,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(reserve_pot_id,[year],[month],expected_amount,actual_amount,
    applied_to_current_month,applied_to_recovery,carried_excess_used,carried_excess_created,
    shortfall_amount,genuine_excess,recovery_balance,carried_excess_balance,[status],is_paused)
VALUES(@potId,@year,@month,@expected,@actual,@currentApplied,@recoveryApplied,@creditUsed,
    @creditCreated,@shortfall,@genuineExcess,@recoveryBalance,@creditBalance,@status,@paused);",
                ("@potId", potId), ("@year", month.Year), ("@month", month.Month),
                ("@expected", expected), ("@actual", actual), ("@currentApplied", appliedToCurrent),
                ("@recoveryApplied", appliedToRecovery), ("@creditUsed", carriedExcessUsed),
                ("@creditCreated", carriedExcessCreated), ("@shortfall", shortfall),
                ("@genuineExcess", genuineExcess), ("@recoveryBalance", outstandingRecovery),
                ("@creditBalance", carriedExcess), ("@status", status), ("@paused", paused));

            if (oldStatuses.TryGetValue((month.Year, month.Month), out var old) && old != status &&
                (status is "Missed" or "Overdue" or "Funded" or "Overfunded" or "Funded from carried excess" or "Paused – voluntary contribution"))
            {
                await AddFinanceEventAsync("Household Reserve", $"Funding{status.Replace(" ", string.Empty).Replace("–", string.Empty)}", "ReservePot", potId,
                    $"{pot.Name}: {status}",
                    $"{month:MMMM yyyy}: expected {expected:C}, dashboard contribution {actual:C}, recovery applied {appliedToRecovery:C}, carried excess used {carriedExcessUsed:C}.",
                    actual, "System");
            }
        }

        await ExecuteAsync("DELETE FROM dbo.reserve_pot_monthly_funding WHERE reserve_pot_id=@id AND DATEFROMPARTS([year],[month],1)<@start", ("@id", potId), ("@start", start));
    }

    private async Task<ReservePotFundingSummary> GetReservePotFundingSummaryAsync(ReservePot pot)
    {
        var months = new List<ReservePotFundingMonth>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT [year],[month],expected_amount,actual_amount,
    applied_to_current_month,applied_to_recovery,carried_excess_used,carried_excess_created,
    shortfall_amount,genuine_excess,recovery_balance,carried_excess_balance,[status],is_paused,updated_at
FROM dbo.reserve_pot_monthly_funding WHERE reserve_pot_id=@id ORDER BY [year],[month]", con);
        cmd.Parameters.AddWithValue("@id", pot.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            months.Add(new ReservePotFundingMonth(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3),
                reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7),
                reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11),
                reader.GetString(12), reader.GetBoolean(13), reader.GetDateTime(14)));
        }

        var recoveryAllocations = new List<ReservePotRecoveryAllocation>();
        await using (var recoveryCon = new SqlConnection(ConnStr))
        {
            await recoveryCon.OpenAsync();
            await using var recoveryCmd = new SqlCommand(@"SELECT source_year,source_month,target_year,target_month,amount,created_at
FROM dbo.reserve_pot_recovery_allocations WHERE reserve_pot_id=@id
ORDER BY source_year DESC,source_month DESC,target_year,target_month", recoveryCon);
            recoveryCmd.Parameters.AddWithValue("@id", pot.Id);
            await using var recoveryReader = await recoveryCmd.ExecuteReaderAsync();
            while (await recoveryReader.ReadAsync())
            {
                recoveryAllocations.Add(new ReservePotRecoveryAllocation(
                    recoveryReader.GetInt32(0), recoveryReader.GetInt32(1),
                    recoveryReader.GetInt32(2), recoveryReader.GetInt32(3),
                    recoveryReader.GetDecimal(4), recoveryReader.GetDateTime(5)));
            }
        }

        var current = months.LastOrDefault(x => x.Year == DateTime.Today.Year && x.Month == DateTime.Today.Month);
        var outstanding = current?.RecoveryBalance ?? 0m;
        var availableCredit = current?.CarriedExcessBalance ?? 0m;
        var expectedBalanceToday = Math.Max(0m, pot.AllocatedAmount + outstanding);
        decimal? requiredMonthly = null, projectedShortfall = null, extraRequired = null;
        decimal projected = pot.AllocatedAmount;
        if (pot.TargetAmount.HasValue && pot.DueDate.HasValue && pot.DueDate.Value.Date >= DateTime.Today)
        {
            var monthsRemaining = Math.Max(1, ((pot.DueDate.Value.Year - DateTime.Today.Year) * 12) + pot.DueDate.Value.Month - DateTime.Today.Month + 1);
            requiredMonthly = Math.Round(Math.Max(0m, pot.TargetAmount.Value - pot.AllocatedAmount) / monthsRemaining, 2);
            projected = Math.Round(pot.AllocatedAmount + pot.IntendedMonthlyContribution * monthsRemaining, 2);
            projectedShortfall = Math.Max(0m, pot.TargetAmount.Value - projected);
            extraRequired = Math.Max(0m, requiredMonthly.Value - pot.IntendedMonthlyContribution);
        }

        var currentMonthStatus = current?.Status ?? (pot.IsActive ? "Not configured" : "Inactive");
        var status = !pot.IsActive
            ? "Inactive"
            : pot.AllocatedAmount < 0m
                ? "Overdrawn"
                : pot.TargetAmount.HasValue && pot.AllocatedAmount >= pot.TargetAmount.Value
                    ? "Completed"
                    : pot.AllocatedAmount == 0m
                        ? "Not started"
                        : "In progress";
        var css = status switch
        {
            "Completed" => "good",
            "Not started" or "In progress" => "warn",
            "Overdrawn" => "bad",
            _ => "muted"
        };

        return new ReservePotFundingSummary
        {
            PotId = pot.Id,
            CurrentStatus = status,
            CurrentMonthStatus = currentMonthStatus,
            StatusCssClass = css,
            CurrentMonthExpected = current?.ExpectedAmount ?? 0m,
            CurrentMonthActual = current?.ActualAmount ?? 0m,
            CurrentMonthEffectiveFunding = current?.EffectiveCurrentMonthFunding ?? 0m,
            CurrentMonthAppliedToRecovery = current?.AppliedToRecovery ?? 0m,
            CurrentMonthCarriedExcessUsed = current?.CarriedExcessUsed ?? 0m,
            CurrentMonthCarriedExcessCreated = current?.CarriedExcessCreated ?? 0m,
            CurrentMonthGenuineExcess = current?.GenuineExcess ?? 0m,
            OutstandingRecovery = outstanding,
            AvailableCarriedExcess = availableCredit,
            ExpectedBalanceToday = expectedBalanceToday,
            ActualBalance = pot.AllocatedAmount,
            MissedMonths = months.Count(x => x.Status == "Missed"),
            PartiallyFundedMonths = months.Count(x => x.Status == "Partially funded"),
            ProjectedBalanceByDueDate = projected,
            ProjectedShortfall = projectedShortfall,
            RequiredMonthlyContribution = requiredMonthly,
            AdditionalMonthlyContributionRequired = extraRequired,
            Months = months.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).ToList(),
            RecoveryAllocations = recoveryAllocations
        };
    }

    public async Task AddFinanceEventAsync(string area, string eventType, string entityType, int? entityId, string title, string? description, decimal? amount, string source)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source) VALUES(@area,@type,@entityType,@entityId,@title,@description,@amount,@source)",
            ("@area",area),("@type",eventType),("@entityType",entityType),("@entityId",entityId.HasValue?entityId.Value:DBNull.Value),("@title",title),("@description",DbValue(description)),("@amount",amount.HasValue?amount.Value:DBNull.Value),("@source",source));
    }

    public async Task<List<FinanceEventRow>> GetFinanceEventsAsync(int? potId = null, string? eventType = null, DateTime? from = null, DateTime? to = null)
    {
        await EnsureModernTablesAsync();
        var list = new List<FinanceEventRow>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        var sql = "SELECT finance_event_id,occurred_at,area,event_type,entity_type,entity_id,title,[description],amount,source FROM dbo.finance_events WHERE (@potId IS NULL OR (entity_type='ReservePot' AND entity_id=@potId)) AND (@eventType IS NULL OR event_type=@eventType) AND (@from IS NULL OR occurred_at>=@from) AND (@to IS NULL OR occurred_at<DATEADD(day,1,@to)) ORDER BY occurred_at DESC";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@potId", potId.HasValue ? potId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@eventType", string.IsNullOrWhiteSpace(eventType) ? DBNull.Value : eventType);
        cmd.Parameters.AddWithValue("@from", from.HasValue ? from.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@to", to.HasValue ? to.Value.Date : DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new FinanceEventRow(r.GetInt64(0),r.GetDateTime(1),r.GetString(2),r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetInt32(5),r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetDecimal(8),r.GetString(9)));
        return list;
    }

    public async Task<List<FinanceReminderRow>> GetFinanceRemindersAsync(string? status = "Open", bool dueOnly = false)
    {
        await EnsureModernTablesAsync();
        var list = new List<FinanceReminderRow>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        var sql = @"SELECT r.finance_reminder_id,r.reserve_pot_id,p.[name],r.title,r.[description],r.due_date,r.reminder_type,r.[status],r.is_system_generated,r.snoozed_until,r.created_at,r.updated_at
FROM dbo.finance_reminders r
LEFT JOIN dbo.reserve_pots p ON p.reserve_pot_id=r.reserve_pot_id
WHERE (@status IS NULL OR r.[status]=@status)
  AND (@dueOnly=0 OR COALESCE(r.snoozed_until,r.due_date)<=CONVERT(date,GETDATE()))
ORDER BY CASE WHEN r.[status]='Open' THEN 0 ELSE 1 END, COALESCE(r.snoozed_until,r.due_date), r.created_at DESC";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) || status.Equals("All", StringComparison.OrdinalIgnoreCase) ? DBNull.Value : status);
        cmd.Parameters.AddWithValue("@dueOnly", dueOnly);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new FinanceReminderRow(
                r.GetInt32(0),
                r.IsDBNull(1) ? null : r.GetInt32(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.GetDateTime(5),
                r.GetString(6),
                r.GetString(7),
                r.GetBoolean(8),
                r.IsDBNull(9) ? null : r.GetDateTime(9),
                r.GetDateTime(10),
                r.GetDateTime(11)));
        }
        return list;
    }

    public async Task<int> GetDueReminderCountAsync()
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync("SELECT COUNT(*) FROM dbo.finance_reminders WHERE [status]='Open' AND COALESCE(snoozed_until,due_date)<=CONVERT(date,GETDATE())");
        return Convert.ToInt32(value ?? 0);
    }

    public async Task AddReminderAsync(int? reservePotId, string title, string? description, DateTime dueDate)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A reminder title is required.");
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"INSERT INTO dbo.finance_reminders(reserve_pot_id,title,[description],due_date,reminder_type,[status],is_system_generated)
VALUES(@potId,@title,@description,@dueDate,'Manual','Open',0)",
            ("@potId", reservePotId.HasValue ? reservePotId.Value : DBNull.Value),
            ("@title", title.Trim()),
            ("@description", DbValue(description)),
            ("@dueDate", dueDate.Date));
        await AddFinanceEventAsync("Reminders", "ReminderCreated", "ReservePot", reservePotId, title.Trim(), description, null, "User");
    }

    public async Task UpdateReminderStatusAsync(int id, string status)
    {
        var allowed = new[] { "Open", "Completed", "Dismissed" };
        if (!allowed.Contains(status)) throw new ArgumentException("Invalid reminder status.");
        await EnsureModernTablesAsync();
        await ExecuteAsync("UPDATE dbo.finance_reminders SET [status]=@status,snoozed_until=NULL,updated_at=SYSUTCDATETIME() WHERE finance_reminder_id=@id", ("@status", status), ("@id", id));
    }

    public async Task SnoozeReminderAsync(int id, DateTime snoozedUntil)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("UPDATE dbo.finance_reminders SET [status]='Open',snoozed_until=@until,updated_at=SYSUTCDATETIME() WHERE finance_reminder_id=@id", ("@until", snoozedUntil.Date), ("@id", id));
    }

    public async Task SyncFundingRemindersAsync(IEnumerable<ReservePot> pots, IReadOnlyDictionary<int, ReservePotFundingSummary> summaries)
    {
        await EnsureModernTablesAsync();
        var today = DateTime.Today;
        foreach (var pot in pots.Where(x => x.IsActive))
        {
            if (!summaries.TryGetValue(pot.Id, out var summary)) continue;

            var negativeKey = $"negative:{pot.Id}";
            if (pot.AllocatedAmount < 0m)
            {
                var negativeAmount = Math.Abs(pot.AllocatedAmount);
                await ExecuteAsync(@"MERGE dbo.finance_reminders AS target
USING (SELECT @key system_key) source ON target.system_key=source.system_key
WHEN MATCHED THEN UPDATE SET reserve_pot_id=@potId,title=@title,[description]=@description,due_date=@dueDate,reminder_type='NegativeBalance',updated_at=SYSUTCDATETIME(),[status]='Open'
WHEN NOT MATCHED THEN INSERT(reserve_pot_id,title,[description],due_date,reminder_type,[status],is_system_generated,system_key)
VALUES(@potId,@title,@description,@dueDate,'NegativeBalance','Open',1,@key);",
                    ("@key", negativeKey),
                    ("@potId", pot.Id),
                    ("@title", $"{pot.Name} is overdrawn"),
                    ("@description", $"The pot requires {negativeAmount:C} to return to £0. Normal monthly funding remains unchanged."),
                    ("@dueDate", today));
            }
            else
            {
                await ExecuteAsync("UPDATE dbo.finance_reminders SET [status]='Completed',snoozed_until=NULL,updated_at=SYSUTCDATETIME() WHERE system_key=@key AND [status]='Open'", ("@key", negativeKey));
            }

            if (summary.CurrentStatus is "Overdue" or "Missed" or "Partially funded")
            {
                var key = $"funding:{pot.Id}:{today:yyyyMM}";
                var title = $"{pot.Name} needs funding attention";
                var description = $"{summary.CurrentStatus}. {summary.CurrentMonthEffectiveFunding:C} of {summary.CurrentMonthExpected:C} funded; {summary.OutstandingRecovery:C} remains to recover.";
                await ExecuteAsync(@"MERGE dbo.finance_reminders AS target
USING (SELECT @key system_key) source ON target.system_key=source.system_key
WHEN MATCHED THEN UPDATE SET reserve_pot_id=@potId,title=@title,[description]=@description,due_date=@dueDate,reminder_type='Funding',updated_at=SYSUTCDATETIME(),[status]=CASE WHEN target.[status]='Completed' THEN 'Completed' ELSE 'Open' END
WHEN NOT MATCHED THEN INSERT(reserve_pot_id,title,[description],due_date,reminder_type,[status],is_system_generated,system_key)
VALUES(@potId,@title,@description,@dueDate,'Funding','Open',1,@key);",
                    ("@key", key), ("@potId", pot.Id), ("@title", title), ("@description", description), ("@dueDate", today));
            }

            if (pot.DueDate.HasValue && pot.TargetAmount.HasValue && pot.AllocatedAmount < pot.TargetAmount.Value)
            {
                var days = (pot.DueDate.Value.Date - today).Days;
                if (days is >= 0 and <= 30)
                {
                    var key = $"target:{pot.Id}:{pot.DueDate.Value:yyyyMMdd}";
                        var title = $"{pot.Name} target is due soon";
                    var description = $"Target date: {pot.DueDate.Value:dd MMM yyyy}. Remaining: {Math.Max(0m, pot.TargetAmount.Value - pot.AllocatedAmount):C}.";
                    await ExecuteAsync(@"MERGE dbo.finance_reminders AS target
USING (SELECT @key system_key) source ON target.system_key=source.system_key
WHEN MATCHED THEN UPDATE SET reserve_pot_id=@potId,title=@title,[description]=@description,due_date=@dueDate,reminder_type='TargetDue',updated_at=SYSUTCDATETIME(),[status]=CASE WHEN target.[status]='Completed' THEN 'Completed' ELSE 'Open' END
WHEN NOT MATCHED THEN INSERT(reserve_pot_id,title,[description],due_date,reminder_type,[status],is_system_generated,system_key)
VALUES(@potId,@title,@description,@dueDate,'TargetDue','Open',1,@key);",
                        ("@key", key), ("@potId", pot.Id), ("@title", title), ("@description", description), ("@dueDate", pot.DueDate.Value.Date));
                }
            }
        }

        await ExecuteAsync("UPDATE dbo.finance_reminders SET [status]='Dismissed',updated_at=SYSUTCDATETIME() WHERE is_system_generated=1 AND [status]='Open' AND system_key LIKE 'funding:%' AND due_date<DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1)");
    }


    public async Task<ApplyRecommendationResult> ApplyRecoveryRecommendationAsync(
        int potId,
        decimal requestedAmount,
        string operationKey)
    {
        if (requestedAmount <= 0m)
            return new ApplyRecommendationResult { PotId = potId, Message = "The amount must be greater than zero." };
        if (string.IsNullOrWhiteSpace(operationKey))
            throw new ArgumentException("An operation key is required.", nameof(operationKey));

        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.Serializable);
        var committed = false;

        try
        {
            await using (var existing = new SqlCommand(@"SELECT a.amount,p.[name]
FROM dbo.recommendation_applications a
INNER JOIN dbo.reserve_pots p ON p.reserve_pot_id=a.reserve_pot_id
WHERE a.operation_key=@key", con, tx))
            {
                existing.Parameters.AddWithValue("@key", operationKey.Trim());
                await using var reader = await existing.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var amount = reader.GetDecimal(0);
                    var name = reader.GetString(1);
                    await reader.DisposeAsync();
                    await tx.CommitAsync();
                    return new ApplyRecommendationResult
                    {
                        Succeeded = true,
                        WasAlreadyApplied = true,
                        PotId = potId,
                        PotName = name,
                        AppliedAmount = amount,
                        Message = "This recommendation was already applied."
                    };
                }
            }

            string potName;
            decimal allocatedAmount;
            decimal? targetAmount;
            await using (var potCmd = new SqlCommand(@"SELECT [name],allocated_amount,target_amount
FROM dbo.reserve_pots WITH (UPDLOCK,HOLDLOCK)
WHERE reserve_pot_id=@potId AND is_active=1", con, tx))
            {
                potCmd.Parameters.AddWithValue("@potId", potId);
                await using var reader = await potCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    await reader.DisposeAsync();
                    await tx.RollbackAsync();
                    return new ApplyRecommendationResult { PotId = potId, Message = "The selected pot is not active or could not be found." };
                }
                potName = reader.GetString(0);
                allocatedAmount = reader.GetDecimal(1);
                targetAmount = reader.IsDBNull(2) ? null : reader.GetDecimal(2);
            }

            decimal available;
            await using (var availableCmd = new SqlCommand(@"SELECT CASE WHEN hr.balance-ISNULL(SUM(CASE WHEN p.is_active=1 THEN p.allocated_amount ELSE 0 END),0)<0 THEN 0
ELSE hr.balance-ISNULL(SUM(CASE WHEN p.is_active=1 THEN p.allocated_amount ELSE 0 END),0) END
FROM dbo.household_reserve hr WITH (UPDLOCK,HOLDLOCK)
LEFT JOIN dbo.reserve_pots p WITH (UPDLOCK,HOLDLOCK) ON 1=1
WHERE hr.household_reserve_id=1
GROUP BY hr.balance", con, tx))
            {
                available = Convert.ToDecimal(await availableCmd.ExecuteScalarAsync() ?? 0m);
            }

            decimal recovery;
            await using (var recoveryCmd = new SqlCommand(@"SELECT ISNULL((SELECT TOP 1 recovery_balance
FROM dbo.reserve_pot_monthly_funding
WHERE reserve_pot_id=@potId
ORDER BY [year] DESC,[month] DESC),0)", con, tx))
            {
                recoveryCmd.Parameters.AddWithValue("@potId", potId);
                recovery = Convert.ToDecimal(await recoveryCmd.ExecuteScalarAsync() ?? 0m);
            }

            var negativeBalanceRecovery = Math.Max(0m, -allocatedAmount);
            var totalRecoveryRequired = negativeBalanceRecovery + recovery;
            var amountToApply = Math.Min(requestedAmount, Math.Min(available, totalRecoveryRequired));
            if (amountToApply <= 0m)
            {
                await tx.RollbackAsync();
                return new ApplyRecommendationResult
                {
                    PotId = potId,
                    PotName = potName,
                    Message = "No amount can be applied because the reserve or recovery balance has changed."
                };
            }

            await using (var apply = new SqlCommand(@"INSERT INTO dbo.savings([name],amount,[date],[length],notes)
VALUES(@name,@amount,CONVERT(date,GETDATE()),'One-off','Applied from unallocated Household Reserve using a recovery recommendation.');

UPDATE dbo.reserve_pots
SET allocated_amount=allocated_amount+@amount,updated_at=SYSUTCDATETIME()
WHERE reserve_pot_id=@potId;

INSERT INTO dbo.recommendation_applications(operation_key,reserve_pot_id,amount)
VALUES(@key,@potId,@amount);

INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source)
VALUES('Household Reserve','RecoveryRecommendationApplied','ReservePot',@potId,@title,@description,@amount,'Recommendation');

UPDATE dbo.finance_reminders
SET [status]='Completed',snoozed_until=NULL,updated_at=SYSUTCDATETIME()
WHERE reserve_pot_id=@potId AND [status]='Open' AND reminder_type='Funding' AND @amount>=@recovery;

UPDATE dbo.finance_reminders
SET [status]='Completed',snoozed_until=NULL,updated_at=SYSUTCDATETIME()
WHERE reserve_pot_id=@potId AND [status]='Open' AND reminder_type='TargetDue'
  AND @targetAmount IS NOT NULL AND @allocatedAfter>=@targetAmount;", con, tx))
            {
                apply.Parameters.AddWithValue("@name", potName);
                apply.Parameters.AddWithValue("@amount", amountToApply);
                apply.Parameters.AddWithValue("@potId", potId);
                apply.Parameters.AddWithValue("@key", operationKey.Trim());
                apply.Parameters.AddWithValue("@title", $"{potName} recovery recommendation applied");
                apply.Parameters.AddWithValue("@description", negativeBalanceRecovery > 0m
                    ? "Unallocated Household Reserve money was deliberately reassigned to restore a negative pot balance and then reduce funding recovery."
                    : "Unallocated Household Reserve money was deliberately reassigned to this pot to reduce outstanding recovery.");
                apply.Parameters.AddWithValue("@recovery", totalRecoveryRequired);
                apply.Parameters.AddWithValue("@targetAmount", targetAmount.HasValue ? targetAmount.Value : DBNull.Value);
                apply.Parameters.AddWithValue("@allocatedAfter", allocatedAmount + amountToApply);
                await apply.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            committed = true;

            await SyncReservePotContributionAverageAsync(potName);
            await RebuildReservePotFundingHistoryAsync(potId);

            return new ApplyRecommendationResult
            {
                Succeeded = true,
                PotId = potId,
                PotName = potName,
                AppliedAmount = amountToApply,
                Message = $"Applied {amountToApply:C} to {potName}."
            };
        }
        catch
        {
            if (!committed) await tx.RollbackAsync();
            throw;
        }
    }


    public async Task<ReservePotActionResult> PayReservePotInFullAsync(int potId, decimal amount, string operationKey)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(operationKey)) throw new ArgumentException("An operation key is required.", nameof(operationKey));

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = await con.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using var existing = new SqlCommand("SELECT action_type,amount,resulting_balance FROM dbo.reserve_pot_actions WHERE operation_key=@key", con, (SqlTransaction)tx);
            existing.Parameters.AddWithValue("@key", operationKey.Trim());
            await using (var reader = await existing.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new ReservePotActionResult { Succeeded = true, WasAlreadyApplied = true, PotId = potId, Amount = reader.GetDecimal(1), ResultingBalance = reader.GetDecimal(2), Message = "This action was already applied." };
                }
            }

            string potName;
            decimal current;
            decimal? target;
            await using (var get = new SqlCommand("SELECT [name],allocated_amount,target_amount FROM dbo.reserve_pots WITH (UPDLOCK,HOLDLOCK) WHERE reserve_pot_id=@id AND is_active=1", con, (SqlTransaction)tx))
            {
                get.Parameters.AddWithValue("@id", potId);
                await using var reader = await get.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return new ReservePotActionResult { PotId = potId, Message = "The pot could not be found or is inactive." };
                potName = reader.GetString(0); current = reader.GetDecimal(1); target = reader.IsDBNull(2) ? null : reader.GetDecimal(2);
            }
            if (!target.HasValue) return new ReservePotActionResult { PotId = potId, PotName = potName, Message = "Set a target before paying this pot in full." };
            var required = Math.Max(0m, target.Value-current);
            var applied = Math.Min(amount, required);
            if (applied <= 0m) return new ReservePotActionResult { PotId=potId, PotName=potName, Message="This pot is already fully funded." };
            var after = current + applied;

            await using var cmd = new SqlCommand(@"INSERT INTO dbo.savings([name],amount,[date],[length],notes) VALUES(@name,@amount,CONVERT(date,GETDATE()),'One-off','Pot paid in full from unallocated reserve surplus.');
UPDATE dbo.reserve_pots SET allocated_amount=@after,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id;
INSERT INTO dbo.reserve_pot_actions(operation_key,reserve_pot_id,action_type,amount,action_date,reason,resulting_balance) VALUES(@key,@id,'PayInFull',@amount,CONVERT(date,GETDATE()),'Pot paid in full',@after);
INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source) VALUES('Household Reserve','PotPaidInFull','ReservePot',@id,@title,@description,@amount,'User');
UPDATE dbo.finance_reminders SET [status]='Completed',snoozed_until=NULL,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id AND [status]='Open' AND reminder_type IN ('Funding','TargetDue');", con, (SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@name",potName); cmd.Parameters.AddWithValue("@amount",applied); cmd.Parameters.AddWithValue("@after",after); cmd.Parameters.AddWithValue("@id",potId); cmd.Parameters.AddWithValue("@key",operationKey.Trim()); cmd.Parameters.AddWithValue("@title",$"{potName} paid in full"); cmd.Parameters.AddWithValue("@description","The remaining target amount was deliberately allocated from reserve surplus.");
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            await SyncReservePotContributionAverageAsync(potName);
            await RebuildReservePotFundingHistoryAsync(potId);
            return new ReservePotActionResult { Succeeded=true, PotId=potId, PotName=potName, Amount=applied, ResultingBalance=after, Message=$"{potName} was paid in full with {applied:C}." };
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<ReservePotActionResult> WithdrawFromReservePotAsync(int potId, decimal amount, DateTime withdrawalDate, string reason, string operationKey)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(operationKey)) throw new ArgumentException("An operation key is required.", nameof(operationKey));
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var tx = await con.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using var duplicate = new SqlCommand("SELECT amount,resulting_balance FROM dbo.reserve_pot_actions WHERE operation_key=@key",con,(SqlTransaction)tx); duplicate.Parameters.AddWithValue("@key",operationKey.Trim());
            await using(var reader=await duplicate.ExecuteReaderAsync()) if(await reader.ReadAsync()) return new ReservePotActionResult { Succeeded=true, WasAlreadyApplied=true, PotId=potId, Amount=reader.GetDecimal(0), ResultingBalance=reader.GetDecimal(1), Message="This withdrawal was already recorded." };
            string name; decimal current;
            await using(var get=new SqlCommand("SELECT [name],allocated_amount FROM dbo.reserve_pots WITH (UPDLOCK,HOLDLOCK) WHERE reserve_pot_id=@id",con,(SqlTransaction)tx)) { get.Parameters.AddWithValue("@id",potId); await using var reader=await get.ExecuteReaderAsync(); if(!await reader.ReadAsync()) return new ReservePotActionResult { PotId=potId, Message="The pot could not be found." }; name=reader.GetString(0); current=reader.GetDecimal(1); }
            var after=current-amount;
            await using var cmd=new SqlCommand(@"UPDATE dbo.reserve_pots SET allocated_amount=@after,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id;
INSERT INTO dbo.reserve_pot_actions(operation_key,reserve_pot_id,action_type,amount,action_date,reason,resulting_balance) VALUES(@key,@id,'Withdrawal',@amount,@date,@reason,@after);
INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source) VALUES('Household Reserve',CASE WHEN @after<0 THEN 'PotOverdrawn' ELSE 'PotWithdrawal' END,'ReservePot',@id,@title,@description,@negativeAmount,'User');",con,(SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@after",after); cmd.Parameters.AddWithValue("@id",potId); cmd.Parameters.AddWithValue("@key",operationKey.Trim()); cmd.Parameters.AddWithValue("@amount",amount); cmd.Parameters.AddWithValue("@date",withdrawalDate.Date); cmd.Parameters.AddWithValue("@reason",reason.Trim()); cmd.Parameters.AddWithValue("@title",$"Withdrawal from {name}"); cmd.Parameters.AddWithValue("@description",$"{amount:C} withdrawn on {withdrawalDate:dd MMM yyyy}. Reason: {reason.Trim()}. Resulting balance: {after:C}."); cmd.Parameters.AddWithValue("@negativeAmount",-amount);
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            await RebuildReservePotFundingHistoryAsync(potId);
            return new ReservePotActionResult { Succeeded=true, PotId=potId, PotName=name, Amount=amount, ResultingBalance=after, Message=after<0 ? $"Withdrew {amount:C} from {name}. The pot is now {after:C}." : $"Withdrew {amount:C} from {name}." };
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<ReservePotActionResult> RestoreNegativeReservePotBalanceAsync(int potId, decimal requestedAmount, string operationKey)
    {
        await EnsureModernTablesAsync();
        if (requestedAmount <= 0m) return new ReservePotActionResult { PotId = potId, Message = "The recovery contribution must be greater than zero." };
        if (string.IsNullOrWhiteSpace(operationKey)) throw new ArgumentException("An operation key is required.", nameof(operationKey));

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = await con.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using (var duplicate = new SqlCommand("SELECT amount,resulting_balance FROM dbo.reserve_pot_actions WHERE operation_key=@key", con, (SqlTransaction)tx))
            {
                duplicate.Parameters.AddWithValue("@key", operationKey.Trim());
                await using var reader = await duplicate.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ReservePotActionResult
                    {
                        Succeeded = true,
                        WasAlreadyApplied = true,
                        PotId = potId,
                        Amount = reader.GetDecimal(0),
                        ResultingBalance = reader.GetDecimal(1),
                        Message = "This recovery contribution was already recorded."
                    };
                }
            }

            string potName;
            decimal currentBalance;
            await using (var get = new SqlCommand("SELECT [name],allocated_amount FROM dbo.reserve_pots WITH (UPDLOCK,HOLDLOCK) WHERE reserve_pot_id=@id AND is_active=1", con, (SqlTransaction)tx))
            {
                get.Parameters.AddWithValue("@id", potId);
                await using var reader = await get.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return new ReservePotActionResult { PotId = potId, Message = "The pot could not be found or is inactive." };
                potName = reader.GetString(0);
                currentBalance = reader.GetDecimal(1);
            }

            if (currentBalance >= 0m) return new ReservePotActionResult { PotId = potId, PotName = potName, Message = "This pot no longer has a negative balance." };

            var amountRequired = Math.Abs(currentBalance);
            var applied = Math.Min(requestedAmount, amountRequired);
            var resultingBalance = currentBalance + applied;
            var restored = resultingBalance >= 0m;
            var eventType = restored ? "NegativeBalanceRestored" : "NegativeBalancePartiallyRestored";

            await using var cmd = new SqlCommand(@"INSERT INTO dbo.savings([name],amount,[date],[length],notes)
VALUES(@name,@amount,CONVERT(date,GETDATE()),'One-off','Additional contribution to restore a negative pot balance.');
UPDATE dbo.reserve_pots SET allocated_amount=@after,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id;
INSERT INTO dbo.reserve_pot_actions(operation_key,reserve_pot_id,action_type,amount,action_date,reason,resulting_balance)
VALUES(@key,@id,'NegativeBalanceRecovery',@amount,CONVERT(date,GETDATE()),'Additional contribution to restore negative balance',@after);
INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source)
VALUES('Household Reserve',@eventType,'ReservePot',@id,@title,@description,@amount,'User');
UPDATE dbo.finance_reminders SET [status]=CASE WHEN @restored=1 THEN 'Completed' ELSE 'Open' END,snoozed_until=NULL,updated_at=SYSUTCDATETIME()
WHERE system_key=@negativeKey;", con, (SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@name", potName);
            cmd.Parameters.AddWithValue("@amount", applied);
            cmd.Parameters.AddWithValue("@after", resultingBalance);
            cmd.Parameters.AddWithValue("@id", potId);
            cmd.Parameters.AddWithValue("@key", operationKey.Trim());
            cmd.Parameters.AddWithValue("@eventType", eventType);
            cmd.Parameters.AddWithValue("@title", restored ? $"{potName} restored to £0" : $"{potName} negative balance reduced");
            cmd.Parameters.AddWithValue("@description", restored
                ? $"An additional {applied:C} contribution restored the pot from {currentBalance:C} to £0."
                : $"An additional {applied:C} contribution reduced the negative balance from {currentBalance:C} to {resultingBalance:C}.");
            cmd.Parameters.AddWithValue("@restored", restored);
            cmd.Parameters.AddWithValue("@negativeKey", $"negative:{potId}");
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();

            await SyncReservePotContributionAverageAsync(potName);
            await RebuildReservePotFundingHistoryAsync(potId);

            return new ReservePotActionResult
            {
                Succeeded = true,
                PotId = potId,
                PotName = potName,
                Amount = applied,
                ResultingBalance = resultingBalance,
                Message = restored
                    ? $"{potName} has been restored to £0 with an additional {applied:C} contribution."
                    : $"Added {applied:C} to {potName}. {Math.Abs(resultingBalance):C} remains to restore the pot to £0."
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteReservePotAsync(int id)
    {
        await EnsureModernTablesAsync();
        var pot = (await GetReservePotsAsync()).FirstOrDefault(x => x.Id == id);
        if (pot is not null) await AddFinanceEventAsync("Household Reserve", "PotDeleted", "ReservePot", id, $"{pot.Name} deleted", "The virtual allocation was deleted.", pot.AllocatedAmount, "User");
        await ExecuteAsync("DELETE FROM dbo.reserve_pots WHERE reserve_pot_id=@id", ("@id",id));
    }

}
