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
CREATE TABLE dbo.savings(savings_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(150) NOT NULL, amount decimal(18,2) NOT NULL, [date] date NOT NULL, [type] nvarchar(80) NULL, [length] nvarchar(50) NULL, notes nvarchar(500) NULL);
IF COL_LENGTH('dbo.savings','type') IS NULL ALTER TABLE dbo.savings ADD [type] nvarchar(80) NULL;
IF COL_LENGTH('dbo.investments','type') IS NULL ALTER TABLE dbo.investments ADD [type] nvarchar(80) NULL;
IF COL_LENGTH('dbo.bills','account_balance_id') IS NULL ALTER TABLE dbo.bills ADD account_balance_id int NULL;
IF COL_LENGTH('dbo.everyday_spending','account_balance_id') IS NULL ALTER TABLE dbo.everyday_spending ADD account_balance_id int NULL;
IF COL_LENGTH('dbo.extra_expenses','account_balance_id') IS NULL ALTER TABLE dbo.extra_expenses ADD account_balance_id int NULL;
IF COL_LENGTH('dbo.investments','account_balance_id') IS NULL ALTER TABLE dbo.investments ADD account_balance_id int NULL;
IF COL_LENGTH('dbo.savings','account_balance_id') IS NULL ALTER TABLE dbo.savings ADD account_balance_id int NULL;


IF OBJECT_ID('dbo.monthly_entry_templates','U') IS NULL
CREATE TABLE dbo.monthly_entry_templates(
    monthly_entry_template_id int IDENTITY(1,1) PRIMARY KEY,
    source nvarchar(40) NOT NULL,
    [name] nvarchar(150) NOT NULL,
    default_amount decimal(18,2) NOT NULL DEFAULT 0,
    category nvarchar(100) NULL,
    [type] nvarchar(80) NULL,
    [length] nvarchar(50) NULL,
    notes nvarchar(500) NULL,
    is_active bit NOT NULL DEFAULT 1,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_monthly_entry_templates UNIQUE(source,[name])
);

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
CREATE TABLE dbo.account_balances(account_balance_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(120) NOT NULL, amount decimal(18,2) NOT NULL, interest_rate decimal(9,4) NOT NULL, monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, include_in_global_goal bit NOT NULL DEFAULT 1, interest_handling nvarchar(40) NOT NULL DEFAULT 'Keep invested', updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF COL_LENGTH('dbo.account_balances','include_in_savings_command') IS NULL ALTER TABLE dbo.account_balances ADD include_in_savings_command bit NOT NULL CONSTRAINT DF_account_balances_include_in_savings_command DEFAULT 0;
IF COL_LENGTH('dbo.account_balances','starting_balance') IS NULL ALTER TABLE dbo.account_balances ADD starting_balance decimal(18,2) NULL;
IF COL_LENGTH('dbo.account_balances','provider') IS NULL ALTER TABLE dbo.account_balances ADD provider nvarchar(120) NOT NULL CONSTRAINT DF_account_balances_provider DEFAULT 'Other';
IF COL_LENGTH('dbo.account_balances','account_type') IS NULL ALTER TABLE dbo.account_balances ADD account_type nvarchar(80) NOT NULL CONSTRAINT DF_account_balances_account_type DEFAULT 'Savings';
IF COL_LENGTH('dbo.account_balances','holding_type') IS NULL ALTER TABLE dbo.account_balances ADD holding_type nvarchar(80) NOT NULL CONSTRAINT DF_account_balances_holding_type DEFAULT 'Cash';
IF COL_LENGTH('dbo.account_balances','tax_treatment') IS NULL ALTER TABLE dbo.account_balances ADD tax_treatment nvarchar(40) NOT NULL CONSTRAINT DF_account_balances_tax_treatment DEFAULT 'Tax Free';
IF COL_LENGTH('dbo.account_balances','tax_rate') IS NULL ALTER TABLE dbo.account_balances ADD tax_rate decimal(9,4) NOT NULL CONSTRAINT DF_account_balances_tax_rate DEFAULT 0;
IF COL_LENGTH('dbo.account_balances','tax_effective_from') IS NULL ALTER TABLE dbo.account_balances ADD tax_effective_from date NULL;
IF COL_LENGTH('dbo.account_balances','interest_handling') IS NULL ALTER TABLE dbo.account_balances ADD interest_handling nvarchar(40) NOT NULL CONSTRAINT DF_account_balances_interest_handling DEFAULT 'Keep invested';
IF COL_LENGTH('dbo.account_balances','purpose') IS NULL ALTER TABLE dbo.account_balances ADD purpose nvarchar(40) NOT NULL CONSTRAINT DF_account_balances_purpose DEFAULT 'General';
IF COL_LENGTH('dbo.account_balances','is_default_emergency_fund_destination') IS NULL ALTER TABLE dbo.account_balances ADD is_default_emergency_fund_destination bit NOT NULL CONSTRAINT DF_account_balances_default_emergency DEFAULT 0;
IF COL_LENGTH('dbo.account_balances','usage_type') IS NULL ALTER TABLE dbo.account_balances ADD usage_type nvarchar(30) NOT NULL CONSTRAINT DF_account_balances_usage_type DEFAULT 'Tracking';
IF COL_LENGTH('dbo.account_balances','last_four_digits') IS NULL ALTER TABLE dbo.account_balances ADD last_four_digits nvarchar(4) NULL;
IF COL_LENGTH('dbo.account_balances','statement_parser') IS NULL ALTER TABLE dbo.account_balances ADD statement_parser nvarchar(80) NOT NULL CONSTRAINT DF_account_balances_statement_parser DEFAULT 'Generic';
IF COL_LENGTH('dbo.account_balances','is_active') IS NULL ALTER TABLE dbo.account_balances ADD is_active bit NOT NULL CONSTRAINT DF_account_balances_is_active DEFAULT 1;

IF OBJECT_ID('dbo.bank_statements','U') IS NULL
CREATE TABLE dbo.bank_statements(
    bank_statement_id bigint IDENTITY(1,1) PRIMARY KEY,
    account_balance_id int NOT NULL,
    [year] int NOT NULL,
    [month] int NOT NULL,
    original_file_name nvarchar(260) NULL,
    stored_file_name nvarchar(260) NULL,
    [status] nvarchar(30) NOT NULL DEFAULT 'Draft',
    uploaded_at datetime2 NULL,
    completed_at datetime2 NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_bank_statements_account FOREIGN KEY(account_balance_id) REFERENCES dbo.account_balances(account_balance_id),
    CONSTRAINT UQ_bank_statements_account_month UNIQUE(account_balance_id,[year],[month])
);

IF OBJECT_ID('dbo.bank_statement_transactions','U') IS NULL
CREATE TABLE dbo.bank_statement_transactions(
    bank_statement_transaction_id bigint IDENTITY(1,1) PRIMARY KEY,
    bank_statement_id bigint NOT NULL,
    transaction_date date NOT NULL,
    [description] nvarchar(300) NOT NULL,
    amount decimal(18,2) NOT NULL,
    direction nvarchar(10) NOT NULL,
    [status] nvarchar(30) NOT NULL DEFAULT 'Unmatched',
    matched_source nvarchar(40) NULL,
    matched_entry_id int NULL,
    match_label nvarchar(300) NULL,
    notes nvarchar(500) NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_statement_transactions_statement FOREIGN KEY(bank_statement_id) REFERENCES dbo.bank_statements(bank_statement_id) ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE purpose='EmergencyFund') AND EXISTS (SELECT 1 FROM dbo.emergency_fund WHERE amount > 0)
BEGIN
    INSERT INTO dbo.account_balances([name],amount,interest_rate,monthly_contribution,include_in_global_goal,starting_balance,provider,account_type,holding_type,tax_treatment,tax_rate,interest_handling,purpose,is_default_emergency_fund_destination)
    SELECT 'Emergency Fund', amount, 3.8, 0, 1, amount, 'Other', 'Savings', 'Cash', 'Tax Free', 0, 'Keep invested', 'EmergencyFund', 1 FROM dbo.emergency_fund;
    UPDATE dbo.emergency_fund SET amount=0,updated_at=SYSUTCDATETIME();
END;
IF EXISTS (SELECT 1 FROM dbo.account_balances WHERE purpose='EmergencyFund') AND NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE purpose='EmergencyFund' AND is_default_emergency_fund_destination=1)
BEGIN
    UPDATE dbo.account_balances SET is_default_emergency_fund_destination=1 WHERE account_balance_id=(SELECT TOP 1 account_balance_id FROM dbo.account_balances WHERE purpose='EmergencyFund' ORDER BY account_balance_id);
END;
UPDATE dbo.account_balances SET starting_balance=amount WHERE starting_balance IS NULL;
ALTER TABLE dbo.account_balances ALTER COLUMN starting_balance decimal(18,2) NOT NULL;

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

IF OBJECT_ID('dbo.monthly_income_entries','U') IS NULL
CREATE TABLE dbo.monthly_income_entries(
    monthly_income_entry_id int IDENTITY(1,1) PRIMARY KEY,
    [name] nvarchar(150) NOT NULL,
    amount decimal(18,2) NOT NULL,
    [date] date NOT NULL,
    category nvarchar(80) NULL,
    notes nvarchar(500) NULL,
    is_recurring bit NOT NULL DEFAULT 0,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_monthly_income_entries_date' AND object_id=OBJECT_ID('dbo.monthly_income_entries'))
CREATE INDEX IX_monthly_income_entries_date ON dbo.monthly_income_entries([date]);


IF OBJECT_ID('dbo.passive_income_records','U') IS NULL
CREATE TABLE dbo.passive_income_records(
    passive_income_record_id int IDENTITY(1,1) PRIMARY KEY,
    source_key nvarchar(120) NOT NULL,
    source_name nvarchar(150) NOT NULL,
    income_type nvarchar(80) NOT NULL,
    [year] int NOT NULL,
    [month] int NOT NULL,
    estimated_amount decimal(18,2) NOT NULL DEFAULT 0,
    actual_amount decimal(18,2) NOT NULL,
    received_date date NOT NULL,
    monthly_income_entry_id int NULL,
    notes nvarchar(500) NULL,
    balance_reconciled_at datetime2 NULL,
    interest_handling nvarchar(40) NOT NULL DEFAULT 'Add to Monthly Income',
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_passive_income_records_source_month UNIQUE(source_key,[year],[month]),
    CONSTRAINT FK_passive_income_records_monthly_income FOREIGN KEY(monthly_income_entry_id)
        REFERENCES dbo.monthly_income_entries(monthly_income_entry_id) ON DELETE SET NULL
);

IF OBJECT_ID('dbo.passive_income_records','U') IS NOT NULL AND COL_LENGTH('dbo.passive_income_records','balance_reconciled_at') IS NULL
    ALTER TABLE dbo.passive_income_records ADD balance_reconciled_at datetime2 NULL;
IF OBJECT_ID('dbo.passive_income_records','U') IS NOT NULL AND COL_LENGTH('dbo.passive_income_records','interest_handling') IS NULL
    ALTER TABLE dbo.passive_income_records ADD interest_handling nvarchar(40) NOT NULL CONSTRAINT DF_passive_income_records_interest_handling DEFAULT 'Add to Monthly Income';
IF OBJECT_ID('dbo.passive_income_records','U') IS NOT NULL AND COL_LENGTH('dbo.passive_income_records','estimated_gross_amount') IS NULL
BEGIN
    ALTER TABLE dbo.passive_income_records ADD estimated_gross_amount decimal(18,2) NULL, estimated_tax_amount decimal(18,2) NULL, actual_gross_amount decimal(18,2) NULL, actual_tax_amount decimal(18,2) NULL;
    UPDATE dbo.passive_income_records
    SET estimated_gross_amount=estimated_amount, estimated_tax_amount=0, actual_gross_amount=actual_amount, actual_tax_amount=0
    WHERE estimated_gross_amount IS NULL;
END;

IF OBJECT_ID('dbo.account_reconciliations','U') IS NULL
CREATE TABLE dbo.account_reconciliations(
    account_reconciliation_id int IDENTITY(1,1) PRIMARY KEY,
    source_key nvarchar(120) NOT NULL,
    account_name nvarchar(160) NOT NULL,
    previous_balance decimal(18,2) NOT NULL,
    reconciled_interest decimal(18,2) NOT NULL,
    new_balance decimal(18,2) NOT NULL,
    reconciled_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

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
CREATE TABLE dbo.reserve_pots(reserve_pot_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(140) NOT NULL, allocated_amount decimal(18,2) NOT NULL DEFAULT 0, starting_amount decimal(18,2) NOT NULL DEFAULT 0, default_monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, intended_monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, funding_frequency nvarchar(30) NOT NULL DEFAULT 'Monthly', expected_funding_day int NULL, carry_forward_shortfalls bit NOT NULL DEFAULT 1, carry_excess_forward bit NOT NULL DEFAULT 0, funding_paused_from date NULL, funding_paused_until date NULL, funding_pause_reason nvarchar(300) NULL, target_amount decimal(18,2) NULL, due_date date NULL, priority int NOT NULL DEFAULT 1, is_active bit NOT NULL DEFAULT 1, notes nvarchar(500) NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF COL_LENGTH('dbo.reserve_pots','starting_amount') IS NULL ALTER TABLE dbo.reserve_pots ADD starting_amount decimal(18,2) NULL;
UPDATE dbo.reserve_pots SET starting_amount=allocated_amount WHERE starting_amount IS NULL;
ALTER TABLE dbo.reserve_pots ALTER COLUMN starting_amount decimal(18,2) NOT NULL;
IF COL_LENGTH('dbo.reserve_pots','intended_monthly_contribution') IS NULL ALTER TABLE dbo.reserve_pots ADD intended_monthly_contribution decimal(18,2) NOT NULL CONSTRAINT DF_reserve_pots_intended_monthly_contribution DEFAULT 0;
IF COL_LENGTH('dbo.reserve_pots','funding_frequency') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_frequency nvarchar(30) NOT NULL CONSTRAINT DF_reserve_pots_funding_frequency DEFAULT 'Monthly';
IF COL_LENGTH('dbo.reserve_pots','expected_funding_day') IS NULL ALTER TABLE dbo.reserve_pots ADD expected_funding_day int NULL;
IF COL_LENGTH('dbo.reserve_pots','carry_forward_shortfalls') IS NULL ALTER TABLE dbo.reserve_pots ADD carry_forward_shortfalls bit NOT NULL CONSTRAINT DF_reserve_pots_carry_forward_shortfalls DEFAULT 1;
IF COL_LENGTH('dbo.reserve_pots','carry_excess_forward') IS NULL ALTER TABLE dbo.reserve_pots ADD carry_excess_forward bit NOT NULL CONSTRAINT DF_reserve_pots_carry_excess_forward DEFAULT 0;
IF COL_LENGTH('dbo.reserve_pots','funding_paused_from') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_paused_from date NULL;
IF COL_LENGTH('dbo.reserve_pots','funding_paused_until') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_paused_until date NULL;
IF COL_LENGTH('dbo.reserve_pots','funding_pause_reason') IS NULL ALTER TABLE dbo.reserve_pots ADD funding_pause_reason nvarchar(300) NULL;

IF OBJECT_ID('dbo.reserve_pot_investment_stages','U') IS NULL
CREATE TABLE dbo.reserve_pot_investment_stages(
    reserve_pot_investment_stage_id int IDENTITY(1,1) PRIMARY KEY,
    reserve_pot_id int NOT NULL,
    stage_order int NOT NULL,
    investment_type nvarchar(80) NOT NULL,
    provider nvarchar(120) NOT NULL DEFAULT 'Other',
    expected_annual_return decimal(9,4) NOT NULL DEFAULT 0,
    start_date date NOT NULL,
    end_date date NULL,
    notes nvarchar(500) NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_reserve_pot_investment_stages_pot FOREIGN KEY(reserve_pot_id) REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE CASCADE,
    CONSTRAINT UQ_reserve_pot_investment_stages_order UNIQUE(reserve_pot_id,stage_order)
);

-- Phase 8.4.1 Batch 1: link dashboard household-reserve allocations to reserve pots by ID.
IF COL_LENGTH('dbo.savings','reserve_pot_id') IS NULL ALTER TABLE dbo.savings ADD reserve_pot_id int NULL;
IF COL_LENGTH('dbo.savings','pot_name_snapshot') IS NULL ALTER TABLE dbo.savings ADD pot_name_snapshot nvarchar(140) NULL;

UPDATE dbo.savings
SET pot_name_snapshot = LEFT(LTRIM(RTRIM([name])), 140)
WHERE pot_name_snapshot IS NULL;

;WITH normalised_pots AS
(
    SELECT LOWER(LTRIM(RTRIM([name]))) AS normalised_name,
           MIN(reserve_pot_id) AS reserve_pot_id,
           COUNT(*) AS match_count
    FROM dbo.reserve_pots
    GROUP BY LOWER(LTRIM(RTRIM([name])))
)
UPDATE s
SET reserve_pot_id = p.reserve_pot_id
FROM dbo.savings s
INNER JOIN normalised_pots p
    ON p.normalised_name = LOWER(LTRIM(RTRIM(s.[name])))
WHERE s.reserve_pot_id IS NULL
  AND p.match_count = 1;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_savings_reserve_pots')
ALTER TABLE dbo.savings WITH CHECK
ADD CONSTRAINT FK_savings_reserve_pots FOREIGN KEY(reserve_pot_id)
REFERENCES dbo.reserve_pots(reserve_pot_id) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_savings_reserve_pot_id' AND object_id=OBJECT_ID('dbo.savings'))
CREATE INDEX IX_savings_reserve_pot_id ON dbo.savings(reserve_pot_id);
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

IF OBJECT_ID('dbo.emergency_fund_transactions','U') IS NULL
CREATE TABLE dbo.emergency_fund_transactions(
    emergency_fund_transaction_id bigint IDENTITY(1,1) PRIMARY KEY,
    transaction_type nvarchar(60) NOT NULL,
    amount decimal(18,2) NOT NULL,
    occurred_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    note nvarchar(500) NULL,
    dashboard_savings_id int NULL,
    finance_event_id bigint NULL,
    reversed_transaction_id bigint NULL,
    reversed_by_transaction_id bigint NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_emergency_fund_transactions_savings FOREIGN KEY(dashboard_savings_id) REFERENCES dbo.savings(savings_id),
    CONSTRAINT FK_emergency_fund_transactions_event FOREIGN KEY(finance_event_id) REFERENCES dbo.finance_events(finance_event_id),
    CONSTRAINT FK_emergency_fund_transactions_reversed FOREIGN KEY(reversed_transaction_id) REFERENCES dbo.emergency_fund_transactions(emergency_fund_transaction_id),
    CONSTRAINT FK_emergency_fund_transactions_reversed_by FOREIGN KEY(reversed_by_transaction_id) REFERENCES dbo.emergency_fund_transactions(emergency_fund_transaction_id)
);

IF COL_LENGTH('dbo.emergency_fund_transactions','account_balance_id') IS NULL ALTER TABLE dbo.emergency_fund_transactions ADD account_balance_id int NULL;

IF OBJECT_ID('dbo.emergency_fund_transactions','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_emergency_fund_transactions_occurred' AND object_id=OBJECT_ID('dbo.emergency_fund_transactions'))
CREATE INDEX IX_emergency_fund_transactions_occurred ON dbo.emergency_fund_transactions(occurred_at DESC);

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

IF OBJECT_ID('dbo.forecast_scenarios','U') IS NULL
CREATE TABLE dbo.forecast_scenarios(
    forecast_scenario_id int IDENTITY(1,1) PRIMARY KEY,
    [name] nvarchar(160) NOT NULL,
    is_preferred bit NOT NULL DEFAULT 0,
    months int NOT NULL DEFAULT 12,
    pot_id int NULL,
    monthly_contribution_override decimal(18,2) NULL,
    target_amount_override decimal(18,2) NULL,
    target_date_override date NULL,
    one_off_contribution decimal(18,2) NOT NULL DEFAULT 0,
    interest_rate_override decimal(9,4) NULL,
    protected_baseline_override decimal(18,2) NULL,
    future_expense_amount decimal(18,2) NOT NULL DEFAULT 0,
    future_expense_date date NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID('dbo.forecast_scenarios','U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_forecast_scenarios_preferred' AND object_id=OBJECT_ID('dbo.forecast_scenarios'))
CREATE INDEX IX_forecast_scenarios_preferred ON dbo.forecast_scenarios(is_preferred, updated_at DESC);";
    await ExecuteAsync(sql);
    }

    public async Task<List<PaymentRow>> GetRowsAsync(string source, int month, int year)
    {
        await EnsureModernTablesAsync();
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Id: "billid", Date: "[date]", Category: "NULL", Type: "type", Length: "length", Notes: "description"),
            "everyday_spending" => (Table: "dbo.everyday_spending", Id: "everyday_spending_id", Date: "[date]", Category: "category", Type: "type", Length: "length", Notes: "description"),
            "extra_expenses" => (Table: "dbo.extra_expenses", Id: "extra_expense_id", Date: "duedate", Category: "category", Type: "type", Length: "length", Notes: "description"),
            "investments" => (Table: "dbo.investments", Id: "investments_id", Date: "[date]", Category: "category", Type: "type", Length: "length", Notes: "notes"),
            "savings" => (Table: "dbo.savings", Id: "savings_id", Date: "[date]", Category: "NULL", Type: "type", Length: "length", Notes: "notes"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var reservePotId = source == "savings" ? "p.reserve_pot_id" : "NULL";
        var potNameSnapshot = source == "savings" ? "p.pot_name_snapshot" : "NULL";
        var currentReservePotName = source == "savings" ? "rp.[name]" : "NULL";
        var tableExpression = source == "savings"
            ? $"{map.Table} p LEFT JOIN dbo.reserve_pots rp ON rp.reserve_pot_id = p.reserve_pot_id LEFT JOIN dbo.account_balances ab ON ab.account_balance_id = p.account_balance_id"
            : $"{map.Table} p LEFT JOIN dbo.account_balances ab ON ab.account_balance_id = p.account_balance_id";

        string sql = $@"SELECT p.{map.Id} AS id, p.[name], p.amount, p.{map.Date} AS [date], {map.Category} AS category, {map.Type} AS [type], p.{map.Length} AS [length], p.{map.Notes} AS notes,
p.account_balance_id, ab.[name] AS account_name, {reservePotId} AS reserve_pot_id, {potNameSnapshot} AS pot_name_snapshot, {currentReservePotName} AS current_reserve_pot_name
FROM {tableExpression} WHERE MONTH(p.{map.Date})=@month AND YEAR(p.{map.Date})=@year ORDER BY p.{map.Date} DESC";
        var rows = new List<PaymentRow>();
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@month", month); cmd.Parameters.AddWithValue("@year", year);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new PaymentRow(
                r.GetInt32(0),
                r.GetString(1),
                r.GetDecimal(2),
                r.GetDateTime(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                source,
                r.IsDBNull(8) ? null : r.GetInt32(8),
                r.IsDBNull(9) ? null : r.GetString(9),
                r.IsDBNull(10) ? null : r.GetInt32(10),
                r.IsDBNull(11) ? null : r.GetString(11),
                r.IsDBNull(12) ? null : r.GetString(12)));
        }
        return rows;
    }

    public async Task<decimal> GetEmergencyFundAsync()
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync("SELECT COALESCE(SUM(amount),0) FROM dbo.account_balances WHERE purpose='EmergencyFund'");
        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    public async Task<DateTime?> GetEmergencyFundUpdatedAsync()
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync("SELECT MAX(updated_at) FROM dbo.account_balances WHERE purpose='EmergencyFund'");
        return value is null or DBNull ? null : Convert.ToDateTime(value);
    }

    public async Task<List<PassiveIncomeEstimate>> GetPassiveIncomeEstimatesAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        var emergencyFund = await GetEmergencyFundAsync();
        var accounts = await GetAccountsAsync(emergencyFund);
        var recorded = await GetPassiveIncomeRecordsAsync(year, month);
        var recordedBySource = recorded.ToDictionary(x => x.SourceKey, StringComparer.OrdinalIgnoreCase);
        var calculationDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));

        return accounts
            .Where(x => x.Amount > 0m && x.InterestRate > 0m)
            .Select(account =>
            {
                var sourceKey = account.Id == 0 ? "emergency-fund" : $"account-{account.Id}";
                recordedBySource.TryGetValue(sourceKey, out var actual);
                var monthlyRate = (decimal)(Math.Pow(1d + ((double)account.InterestRate / 100d), 1d / 12d) - 1d);
                var grossEstimate = Math.Round(account.Amount * monthlyRate, 2, MidpointRounding.AwayFromZero);
                var estimatedTax = CalculateInterestTax(account, grossEstimate, calculationDate);
                var netEstimate = Math.Round(grossEstimate - estimatedTax, 2, MidpointRounding.AwayFromZero);

                return new PassiveIncomeEstimate(
                    actual?.Id,
                    sourceKey,
                    account.Name,
                    account.Amount,
                    account.InterestRate,
                    actual?.EstimatedGrossAmount ?? grossEstimate,
                    actual?.EstimatedTaxAmount ?? estimatedTax,
                    actual?.EstimatedAmount ?? netEstimate,
                    actual?.ActualGrossAmount,
                    actual?.ActualTaxAmount,
                    actual?.ActualAmount,
                    account.TaxTreatment,
                    account.TaxRate,
                    account.TaxEffectiveFrom,
                    actual?.ReceivedDate,
                    actual?.IncomeEntryId,
                    actual?.IsBalanceReconciled ?? false,
                    actual?.InterestHandling ?? account.InterestHandling);
            })
            .OrderBy(x => x.SourceName)
            .ToList();
    }

    private static decimal CalculateInterestTax(AccountBalance account, decimal grossAmount, DateTime paymentDate)
    {
        var taxable = account.TaxTreatment.Equals("Taxable", StringComparison.OrdinalIgnoreCase)
            && (!account.TaxEffectiveFrom.HasValue || paymentDate.Date >= account.TaxEffectiveFrom.Value.Date);
        if (!taxable || grossAmount <= 0m) return 0m;
        return Math.Round(grossAmount * (Math.Clamp(account.TaxRate, 0m, 100m) / 100m), 2, MidpointRounding.AwayFromZero);
    }

    public async Task<List<PassiveIncomeRecord>> GetPassiveIncomeRecordsAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        var records = new List<PassiveIncomeRecord>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT passive_income_record_id,source_key,source_name,income_type,[year],[month],estimated_amount,actual_amount,
COALESCE(estimated_gross_amount,estimated_amount),COALESCE(estimated_tax_amount,0),COALESCE(actual_gross_amount,actual_amount),COALESCE(actual_tax_amount,0),
received_date,monthly_income_entry_id,notes,balance_reconciled_at,interest_handling
FROM dbo.passive_income_records WHERE [year]=@year AND [month]=@month ORDER BY received_date,source_name", con);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new PassiveIncomeRecord(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetDecimal(6), reader.GetDecimal(7),
                reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11),
                reader.GetDateTime(12), reader.IsDBNull(13) ? null : reader.GetInt32(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                !reader.IsDBNull(15),
                reader.GetString(16)));
        }
        return records;
    }

    public async Task RecordInterestIncomeAsync(
        string sourceKey,
        string sourceName,
        decimal estimatedGrossAmount,
        decimal actualGrossAmount,
        DateTime receivedDate,
        string? notes,
        string interestHandling)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("A passive-income source is required.");
        if (actualGrossAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(actualGrossAmount));

        interestHandling = NormalizeInterestHandling(interestHandling);

        await EnsureModernTablesAsync();
        var emergencyFund = await GetEmergencyFundAsync();
        var accounts = await GetAccountsAsync(emergencyFund);
        var account = accounts.FirstOrDefault(x => (x.Id == 0 ? "emergency-fund" : $"account-{x.Id}").Equals(sourceKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The interest account could not be found.");
        var estimatedTaxAmount = CalculateInterestTax(account, estimatedGrossAmount, receivedDate);
        var estimatedNetAmount = Math.Round(estimatedGrossAmount - estimatedTaxAmount, 2, MidpointRounding.AwayFromZero);
        var actualTaxAmount = CalculateInterestTax(account, actualGrossAmount, receivedDate);
        var actualNetAmount = Math.Round(actualGrossAmount - actualTaxAmount, 2, MidpointRounding.AwayFromZero);
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var transaction = (SqlTransaction)await con.BeginTransactionAsync();
        try
        {
            await using var existing = new SqlCommand(@"SELECT COUNT(*) FROM dbo.passive_income_records
WHERE source_key=@sourceKey AND [year]=@year AND [month]=@month", con, transaction);
            existing.Parameters.AddWithValue("@sourceKey", sourceKey);
            existing.Parameters.AddWithValue("@year", receivedDate.Year);
            existing.Parameters.AddWithValue("@month", receivedDate.Month);
            var existingCount = Convert.ToInt32(await existing.ExecuteScalarAsync());
            if (existingCount > 0)
                throw new InvalidOperationException($"{sourceName} interest has already been recorded for {receivedDate:MMMM yyyy}.");

            int? incomeEntryId = null;
            if (interestHandling == "Add to Monthly Income")
            {
                await using var income = new SqlCommand(@"INSERT INTO dbo.monthly_income_entries([name],amount,[date],category,notes,is_recurring)
VALUES(@name,@amount,@date,'Interest',@notes,0); SELECT CAST(SCOPE_IDENTITY() AS int);", con, transaction);
                income.Parameters.AddWithValue("@name", $"{sourceName} interest");
                income.Parameters.AddWithValue("@amount", actualNetAmount);
                income.Parameters.AddWithValue("@date", receivedDate.Date);
                income.Parameters.AddWithValue("@notes", DbValue(notes));
                incomeEntryId = Convert.ToInt32(await income.ExecuteScalarAsync());
            }

            await using var passive = new SqlCommand(@"INSERT INTO dbo.passive_income_records(source_key,source_name,income_type,[year],[month],estimated_amount,actual_amount,estimated_gross_amount,estimated_tax_amount,actual_gross_amount,actual_tax_amount,received_date,monthly_income_entry_id,notes,interest_handling)
VALUES(@sourceKey,@sourceName,'Interest',@year,@month,@estimatedNet,@actualNet,@estimatedGross,@estimatedTax,@actualGross,@actualTax,@date,@incomeEntryId,@notes,@interestHandling)", con, transaction);
            passive.Parameters.AddWithValue("@sourceKey", sourceKey);
            passive.Parameters.AddWithValue("@sourceName", sourceName.Trim());
            passive.Parameters.AddWithValue("@year", receivedDate.Year);
            passive.Parameters.AddWithValue("@month", receivedDate.Month);
            passive.Parameters.AddWithValue("@estimatedNet", estimatedNetAmount);
            passive.Parameters.AddWithValue("@actualNet", actualNetAmount);
            passive.Parameters.AddWithValue("@estimatedGross", estimatedGrossAmount);
            passive.Parameters.AddWithValue("@estimatedTax", estimatedTaxAmount);
            passive.Parameters.AddWithValue("@actualGross", actualGrossAmount);
            passive.Parameters.AddWithValue("@actualTax", actualTaxAmount);
            passive.Parameters.AddWithValue("@date", receivedDate.Date);
            passive.Parameters.AddWithValue("@incomeEntryId", incomeEntryId.HasValue ? incomeEntryId.Value : DBNull.Value);
            passive.Parameters.AddWithValue("@notes", DbValue(notes));
            passive.Parameters.AddWithValue("@interestHandling", interestHandling);
            await passive.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        if (interestHandling == "Add to Monthly Income")
            await SyncMonthlyIncomeTotalAsync(receivedDate.Year, receivedDate.Month);
    }

    public async Task UpdatePendingInterestHandlingAsync(int recordId, string interestHandling)
    {
        interestHandling = NormalizeInterestHandling(interestHandling);

        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var transaction = (SqlTransaction)await con.BeginTransactionAsync();

        int year;
        int month;
        string sourceName;
        decimal actualAmount;
        DateTime receivedDate;
        string? notes;
        string currentHandling;
        int? incomeEntryId;
        bool isBalanceReconciled;

        try
        {
            await using (var get = new SqlCommand(@"SELECT source_name,[year],[month],actual_amount,received_date,notes,interest_handling,monthly_income_entry_id,balance_reconciled_at
FROM dbo.passive_income_records WHERE passive_income_record_id=@id", con, transaction))
            {
                get.Parameters.AddWithValue("@id", recordId);
                await using var reader = await get.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("The interest payment could not be found.");

                sourceName = reader.GetString(0);
                year = reader.GetInt32(1);
                month = reader.GetInt32(2);
                actualAmount = reader.GetDecimal(3);
                receivedDate = reader.GetDateTime(4);
                notes = reader.IsDBNull(5) ? null : reader.GetString(5);
                currentHandling = reader.GetString(6);
                incomeEntryId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                isBalanceReconciled = !reader.IsDBNull(8);
            }

            if (incomeEntryId.HasValue)
                throw new InvalidOperationException("This interest has already been added to monthly income and its handling can no longer be changed.");

            if (isBalanceReconciled)
                throw new InvalidOperationException("This interest has already been applied during an account balance update and its handling can no longer be changed.");

            if (string.Equals(currentHandling, interestHandling, StringComparison.OrdinalIgnoreCase))
            {
                await transaction.CommitAsync();
                return;
            }

            int? newIncomeEntryId = null;
            if (interestHandling == "Add to Monthly Income")
            {
                await using var income = new SqlCommand(@"INSERT INTO dbo.monthly_income_entries([name],amount,[date],category,notes,is_recurring)
VALUES(@name,@amount,@date,'Interest',@notes,0); SELECT CAST(SCOPE_IDENTITY() AS int);", con, transaction);
                income.Parameters.AddWithValue("@name", $"{sourceName} interest");
                income.Parameters.AddWithValue("@amount", actualAmount);
                income.Parameters.AddWithValue("@date", receivedDate.Date);
                income.Parameters.AddWithValue("@notes", DbValue(notes));
                newIncomeEntryId = Convert.ToInt32(await income.ExecuteScalarAsync());
            }

            await using var update = new SqlCommand(@"UPDATE dbo.passive_income_records
SET interest_handling=@interestHandling, monthly_income_entry_id=@incomeEntryId
WHERE passive_income_record_id=@id AND monthly_income_entry_id IS NULL AND balance_reconciled_at IS NULL", con, transaction);
            update.Parameters.AddWithValue("@interestHandling", interestHandling);
            update.Parameters.AddWithValue("@incomeEntryId", newIncomeEntryId.HasValue ? newIncomeEntryId.Value : DBNull.Value);
            update.Parameters.AddWithValue("@id", recordId);
            if (await update.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("The interest handling changed elsewhere and could not be updated safely.");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        if (interestHandling == "Add to Monthly Income")
            await SyncMonthlyIncomeTotalAsync(year, month);
    }

    private static string NormalizeInterestHandling(string? value)
    {
        if (string.Equals(value, "Add to Monthly Income", StringComparison.OrdinalIgnoreCase)) return "Add to Monthly Income";
        return "Keep invested";
    }

    public async Task<AccountReconciliationViewModel> GetAccountReconciliationAsync()
    {
        await EnsureModernTablesAsync();
        var emergencyFund = await GetEmergencyFundAsync();
        var accounts = await GetAccountsAsync(emergencyFund);
        var rows = new List<AccountReconciliationRow>();
        var tolerance = Math.Max(0m, await GetDecimalSettingAsync("AccountBalanceTolerance", 5m));
        var baseReserveAmount = Math.Max(0m, await GetDecimalSettingAsync("EmergencyFundBaseline", 12000m));
        var selectedAccountIds = await GetSelectedReserveAccountIdsAsync();
        var selectedAccountsTotal = Math.Round(accounts
            .Where(x => x.TracksBalances && (selectedAccountIds.Contains(x.Id) || x.Purpose == "EmergencyFund"))
            .Sum(x => x.Amount), 2);
        var loggedVirtualPotsTotal = Math.Round(Convert.ToDecimal(await ScalarAsync(
            "SELECT COALESCE(SUM(allocated_amount),0) FROM dbo.reserve_pots WHERE is_active=1")), 2);
        var expectedReserveTotal = Math.Round(baseReserveAmount + loggedVirtualPotsTotal, 2);
        var reserveDifference = Math.Round(selectedAccountsTotal - expectedReserveTotal, 2);
        var reserveStatus = GetReconciliationStatus(reserveDifference, tolerance);

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();

        foreach (var account in accounts.Where(x => x.IsActive && x.Amount >= 0m))
        {
            var sourceKey = account.Id == 0 ? "emergency-fund" : $"account-{account.Id}";
            await using var cmd = new SqlCommand(
                "SELECT MAX(reconciled_at) FROM dbo.account_reconciliations WHERE source_key=@sourceKey", con);
            cmd.Parameters.AddWithValue("@sourceKey", sourceKey);
            var lastReconciledValue = await cmd.ExecuteScalarAsync();
            var lastReconciled = lastReconciledValue is null or DBNull
                ? (DateTime?)null
                : Convert.ToDateTime(lastReconciledValue);

            await using var pendingCmd = new SqlCommand(@"SELECT COALESCE(SUM(actual_amount),0), COUNT(*), MIN(received_date)
FROM dbo.passive_income_records
WHERE source_key=@sourceKey AND income_type='Interest' AND balance_reconciled_at IS NULL", con);
            pendingCmd.Parameters.AddWithValue("@sourceKey", sourceKey);
            await using var pendingReader = await pendingCmd.ExecuteReaderAsync();
            await pendingReader.ReadAsync();
            var pendingInterest = pendingReader.GetDecimal(0);
            var pendingCount = pendingReader.GetInt32(1);
            var oldestPendingInterestDate = pendingReader.IsDBNull(2)
                ? (DateTime?)null
                : pendingReader.GetDateTime(2);
            await pendingReader.CloseAsync();

            var recentActivities = new List<AccountRecentActivityRow>();
            await using var activityCmd = new SqlCommand(@"SELECT TOP 5 occurred_at,title,event_type,amount,source
FROM dbo.finance_events
WHERE area IN ('Household Reserve','Accounts & Settings')
ORDER BY occurred_at DESC", con);
            await using var activityReader = await activityCmd.ExecuteReaderAsync();
            while (await activityReader.ReadAsync())
            {
                recentActivities.Add(new AccountRecentActivityRow(
                    activityReader.GetDateTime(0), activityReader.GetString(1), activityReader.GetString(2),
                    activityReader.IsDBNull(3) ? null : activityReader.GetDecimal(3), activityReader.GetString(4)));
            }
            await activityReader.CloseAsync();

            rows.Add(new AccountReconciliationRow(
                account.Id,
                sourceKey,
                account.Name,
                account.Amount,
                account.InterestRate,
                account.MonthlyContribution,
                account.IncludeInGlobalGoal,
                account.StartingBalance,
                account.Provider,
                account.AccountType,
                account.HoldingType,
                account.TaxTreatment,
                account.TaxRate,
                account.TaxEffectiveFrom,
                lastReconciled,
                account.UpdatedAt,
                Math.Round(pendingInterest, 2),
                pendingCount,
                oldestPendingInterestDate,
                account.InterestHandling,
                account.Purpose,
                account.IsDefaultEmergencyFundDestination,
                account.UsageType,
                account.LastFourDigits,
                account.StatementParser,
                account.IsActive,
                account.Amount,
                0m,
                (selectedAccountIds.Contains(account.Id) || account.Purpose == "EmergencyFund") ? "Included in combined check" : "Not selected",
                recentActivities));
        }

        var emergencyFundTransactions = await GetEmergencyFundTransactionsAsync(con);
        var emergencyAccounts = accounts.Where(x => x.TracksBalances && x.Purpose == "EmergencyFund").ToList();
        var emergencyFundTotal = Math.Round(emergencyAccounts.Sum(x => x.Amount), 2);
        var defaultEmergencyAccountName = emergencyAccounts.FirstOrDefault(x => x.IsDefaultEmergencyFundDestination)?.Name;

        return new AccountReconciliationViewModel
        {
            EmergencyFundTotal = emergencyFundTotal,
            DefaultEmergencyFundAccountName = defaultEmergencyAccountName,
            EmergencyFundTransactions = emergencyFundTransactions,
            BalanceTolerance = tolerance,
            SelectedAccountsTotal = selectedAccountsTotal,
            BaseReserveAmount = baseReserveAmount,
            LoggedVirtualPotsTotal = loggedVirtualPotsTotal,
            ExpectedReserveTotal = expectedReserveTotal,
            ReserveDifference = reserveDifference,
            ReserveReconciliationStatus = reserveStatus,
            Accounts = rows.OrderBy(x => x.AccountName).ToList()
        };
    }

    private static string GetReconciliationStatus(decimal difference, decimal tolerance)
        => Math.Abs(difference) <= 0.01m
            ? "Balanced"
            : Math.Abs(difference) <= tolerance
                ? "Within expected interest variance"
                : difference > 0m ? "More than logged" : "Less than logged";


    public async Task<AccountBalanceUpdateResult> UpdateReconciledAccountBalanceAsync(string sourceKey, decimal actualBalance, bool reconcilePendingInterest)
    {
        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();

        try
        {
            decimal previousBalance;
            string accountName;

            if (sourceKey == "emergency-fund")
            {
                await using var get = new SqlCommand(
                    "SELECT TOP 1 amount FROM dbo.emergency_fund ORDER BY updated_at DESC", con, tx);
                var value = await get.ExecuteScalarAsync();
                if (value is null or DBNull)
                    throw new InvalidOperationException("The Emergency Fund balance could not be found.");

                previousBalance = Convert.ToDecimal(value);
                accountName = "Emergency Fund";

                await using var update = new SqlCommand(
                    "UPDATE dbo.emergency_fund SET amount=@amount, updated_at=SYSUTCDATETIME()", con, tx);
                update.Parameters.AddWithValue("@amount", actualBalance);
                await update.ExecuteNonQueryAsync();
            }
            else if (sourceKey.StartsWith("account-", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(sourceKey[8..], out var accountId))
            {
                await using var get = new SqlCommand(
                    "SELECT name, amount FROM dbo.account_balances WHERE account_balance_id=@id", con, tx);
                get.Parameters.AddWithValue("@id", accountId);
                await using var reader = await get.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("The account balance could not be found.");

                accountName = reader.GetString(0);
                previousBalance = reader.GetDecimal(1);
                await reader.CloseAsync();

                await using var update = new SqlCommand(
                    "UPDATE dbo.account_balances SET amount=@amount, updated_at=SYSUTCDATETIME() WHERE account_balance_id=@id", con, tx);
                update.Parameters.AddWithValue("@amount", actualBalance);
                update.Parameters.AddWithValue("@id", accountId);
                await update.ExecuteNonQueryAsync();
            }
            else
            {
                throw new InvalidOperationException("The selected account could not be found.");
            }

            decimal reconciledInterest = 0m;
            if (reconcilePendingInterest)
            {
                await using var pending = new SqlCommand(@"SELECT COALESCE(SUM(actual_amount),0)
FROM dbo.passive_income_records
WHERE source_key=@sourceKey AND income_type='Interest' AND balance_reconciled_at IS NULL", con, tx);
                pending.Parameters.AddWithValue("@sourceKey", sourceKey);
                reconciledInterest = Convert.ToDecimal(await pending.ExecuteScalarAsync());

                if (reconciledInterest > 0m)
                {
                    await using var mark = new SqlCommand(@"UPDATE dbo.passive_income_records
SET balance_reconciled_at=SYSUTCDATETIME(), updated_at=SYSUTCDATETIME()
WHERE source_key=@sourceKey AND income_type='Interest' AND balance_reconciled_at IS NULL", con, tx);
                    mark.Parameters.AddWithValue("@sourceKey", sourceKey);
                    await mark.ExecuteNonQueryAsync();
                }
            }

            await using var log = new SqlCommand(@"INSERT INTO dbo.account_reconciliations
(source_key, account_name, previous_balance, reconciled_interest, new_balance)
VALUES(@sourceKey, @name, @previous, @interest, @new)", con, tx);
            log.Parameters.AddWithValue("@sourceKey", sourceKey);
            log.Parameters.AddWithValue("@name", accountName);
            log.Parameters.AddWithValue("@previous", previousBalance);
            log.Parameters.AddWithValue("@interest", reconciledInterest);
            log.Parameters.AddWithValue("@new", actualBalance);
            await log.ExecuteNonQueryAsync();

            var tolerance = Math.Max(0m, await GetDecimalSettingAsync("AccountBalanceTolerance", 5m));
            var baseReserveAmount = Math.Max(0m, await GetDecimalSettingAsync("EmergencyFundBaseline", 12000m));
            var selectedAccountIds = await GetSelectedReserveAccountIdsAsync();

            decimal selectedAccountsTotal = 0m;
            if (selectedAccountIds.Contains(0))
            {
                await using var emergencyTotalCmd = new SqlCommand(
                    "SELECT COALESCE((SELECT TOP 1 amount FROM dbo.emergency_fund ORDER BY updated_at DESC),0)", con, tx);
                selectedAccountsTotal += Convert.ToDecimal(await emergencyTotalCmd.ExecuteScalarAsync());
            }

            if (selectedAccountIds.Any(x => x > 0))
            {
                var selectedIds = selectedAccountIds.Where(x => x > 0).ToArray();
                var parameterNames = selectedIds.Select((_, index) => $"@selectedId{index}").ToArray();
                await using var selectedTotalCmd = new SqlCommand(
                    $"SELECT COALESCE(SUM(amount),0) FROM dbo.account_balances WHERE account_balance_id IN ({string.Join(",", parameterNames)})",
                    con,
                    tx);
                for (var index = 0; index < selectedIds.Length; index++)
                    selectedTotalCmd.Parameters.AddWithValue(parameterNames[index], selectedIds[index]);
                selectedAccountsTotal += Convert.ToDecimal(await selectedTotalCmd.ExecuteScalarAsync());
            }

            await using var potsTotalCmd = new SqlCommand(
                "SELECT COALESCE(SUM(allocated_amount),0) FROM dbo.reserve_pots WHERE is_active=1", con, tx);
            var loggedVirtualPotsTotal = Convert.ToDecimal(await potsTotalCmd.ExecuteScalarAsync());
            var expectedBalance = Math.Round(baseReserveAmount + loggedVirtualPotsTotal, 2);
            var difference = Math.Round(selectedAccountsTotal - expectedBalance, 2);
            var status = GetReconciliationStatus(difference, tolerance);

            await using var eventCmd = new SqlCommand(@"INSERT INTO dbo.finance_events(area,event_type,entity_type,title,[description],amount,source)
VALUES('Accounts & Settings','BalanceReconciled','HouseholdReserve',@title,@description,@amount,'User')", con, tx);
            eventCmd.Parameters.AddWithValue("@title", $"{accountName} balance updated");
            eventCmd.Parameters.AddWithValue("@description", $"Combined reserve check: {status}. Selected accounts {selectedAccountsTotal:C}; base reserve {baseReserveAmount:C}; logged pots {loggedVirtualPotsTotal:C}; difference {difference:C}.");
            eventCmd.Parameters.AddWithValue("@amount", actualBalance);
            await eventCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return new AccountBalanceUpdateResult(Math.Round(reconciledInterest, 2), expectedBalance, selectedAccountsTotal, difference, status, accountName);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<int>> GetSelectedReserveAccountIdsAsync()
    {
        var ids = new List<int>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT account_id FROM dbo.reserve_account_selections ORDER BY display_order, account_id", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        return ids;
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

    public async Task<List<MonthlyIncomeEntry>> GetMonthlyIncomeEntriesAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        var entries = new List<MonthlyIncomeEntry>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT monthly_income_entry_id,[name],amount,[date],category,notes,is_recurring
FROM dbo.monthly_income_entries
WHERE YEAR([date])=@year AND MONTH([date])=@month
ORDER BY [date],[name]", con);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new MonthlyIncomeEntry(
                reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDateTime(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetBoolean(6)));
        }
        return entries;
    }

    public async Task<decimal> GetInterestIncomeTotalAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync(@"SELECT COALESCE(SUM(amount), 0)
FROM dbo.monthly_income_entries
WHERE YEAR([date])=@year AND MONTH([date])=@month
  AND LOWER(LTRIM(RTRIM(COALESCE(category, ''))))='interest'",
            ("@year", year), ("@month", month));
        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    public async Task<decimal> GetInterestIncomeTotalAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync(@"SELECT COALESCE(SUM(amount), 0)
FROM dbo.monthly_income_entries
WHERE [date] >= @from AND [date] < @to
  AND LOWER(LTRIM(RTRIM(COALESCE(category, ''))))='interest'",
            ("@from", fromInclusive.Date), ("@to", toExclusive.Date));
        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    public async Task<decimal> GetPassiveInterestTotalAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync(@"SELECT COALESCE(SUM(actual_amount), 0)
FROM dbo.passive_income_records
WHERE received_date >= @from AND received_date < @to AND income_type='Interest'",
            ("@from", fromInclusive.Date), ("@to", toExclusive.Date));
        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    public async Task<decimal?> GetMonthlyIncomeEntriesTotalAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync(@"SELECT CASE WHEN COUNT(*)=0 THEN NULL ELSE SUM(amount) END
FROM dbo.monthly_income_entries WHERE YEAR([date])=@year AND MONTH([date])=@month", ("@year", year), ("@month", month));
        return value is null or DBNull ? null : Convert.ToDecimal(value);
    }

    public async Task<List<MonthlyIncomeEntry>> GetMissingRecurringIncomeEntriesAsync(int year, int month)
    {
        var previous = new DateTime(year, month, 1).AddMonths(-1);
        await EnsureModernTablesAsync();
        var entries = new List<MonthlyIncomeEntry>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT p.monthly_income_entry_id,p.[name],p.amount,p.[date],p.category,p.notes,p.is_recurring
FROM dbo.monthly_income_entries p
WHERE YEAR(p.[date])=@previousYear AND MONTH(p.[date])=@previousMonth
  AND p.is_recurring=1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.monthly_income_entries currentMonth
      WHERE YEAR(currentMonth.[date])=@year AND MONTH(currentMonth.[date])=@month
        AND LOWER(LTRIM(RTRIM(currentMonth.[name])))=LOWER(LTRIM(RTRIM(p.[name])))
  )
ORDER BY p.[name]", con);
        cmd.Parameters.AddWithValue("@previousYear", previous.Year);
        cmd.Parameters.AddWithValue("@previousMonth", previous.Month);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new MonthlyIncomeEntry(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDateTime(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6)));
        }
        return entries;
    }

    public async Task AddMonthlyIncomeEntryAsync(string name, decimal amount, DateTime date, string? category, string? notes, bool isRecurring)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"INSERT INTO dbo.monthly_income_entries([name],amount,[date],category,notes,is_recurring)
VALUES(@name,@amount,@date,@category,@notes,@isRecurring)",
            ("@name", name.Trim()), ("@amount", amount), ("@date", date.Date), ("@category", DbValue(category)), ("@notes", DbValue(notes)), ("@isRecurring", isRecurring));
        await SyncMonthlyIncomeTotalAsync(date.Year, date.Month);
    }

    public async Task UpdateMonthlyIncomeEntryAsync(int id, string name, decimal amount, DateTime date, string? category, string? notes, bool isRecurring)
    {
        await EnsureModernTablesAsync();
        var oldDateValue = await ScalarAsync("SELECT [date] FROM dbo.monthly_income_entries WHERE monthly_income_entry_id=@id", ("@id", id));
        await ExecuteAsync(@"UPDATE dbo.monthly_income_entries SET [name]=@name,amount=@amount,[date]=@date,category=@category,notes=@notes,is_recurring=@isRecurring,updated_at=SYSUTCDATETIME()
WHERE monthly_income_entry_id=@id", ("@id", id), ("@name", name.Trim()), ("@amount", amount), ("@date", date.Date), ("@category", DbValue(category)), ("@notes", DbValue(notes)), ("@isRecurring", isRecurring));
        await ExecuteAsync(@"UPDATE dbo.passive_income_records
SET actual_amount=@amount, received_date=@date, [year]=YEAR(@date), [month]=MONTH(@date), notes=@notes, updated_at=SYSUTCDATETIME()
WHERE monthly_income_entry_id=@id", ("@id", id), ("@amount", amount), ("@date", date.Date), ("@notes", DbValue(notes)));
        if (oldDateValue is not null and not DBNull)
        {
            var oldDate = Convert.ToDateTime(oldDateValue);
            await SyncMonthlyIncomeTotalAsync(oldDate.Year, oldDate.Month);
        }
        await SyncMonthlyIncomeTotalAsync(date.Year, date.Month);
    }

    public async Task DeleteMonthlyIncomeEntryAsync(int id)
    {
        await EnsureModernTablesAsync();
        var dateValue = await ScalarAsync("SELECT [date] FROM dbo.monthly_income_entries WHERE monthly_income_entry_id=@id", ("@id", id));
        await ExecuteAsync("DELETE FROM dbo.passive_income_records WHERE monthly_income_entry_id=@id", ("@id", id));
        await ExecuteAsync("DELETE FROM dbo.monthly_income_entries WHERE monthly_income_entry_id=@id", ("@id", id));
        if (dateValue is not null and not DBNull)
        {
            var date = Convert.ToDateTime(dateValue);
            await SyncMonthlyIncomeTotalAsync(date.Year, date.Month);
        }
    }

    public async Task SetMonthlyIncomeRecurringAsync(int id, bool isRecurring)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("UPDATE dbo.monthly_income_entries SET is_recurring=@isRecurring,updated_at=SYSUTCDATETIME() WHERE monthly_income_entry_id=@id", ("@id", id), ("@isRecurring", isRecurring));
    }

    public async Task<int> SetupRecurringIncomeAsync(int year, int month, IReadOnlyCollection<int> entryIds)
    {
        if (entryIds.Count == 0) return 0;
        var previous = new DateTime(year, month, 1).AddMonths(-1);
        var added = 0;
        foreach (var id in entryIds.Distinct())
        {
            await using var con = new SqlConnection(ConnStr);
            await con.OpenAsync();
            await using var cmd = new SqlCommand(@"INSERT INTO dbo.monthly_income_entries([name],amount,[date],category,notes,is_recurring)
SELECT p.[name],p.amount,@date,p.category,p.notes,1
FROM dbo.monthly_income_entries p
WHERE p.monthly_income_entry_id=@id AND p.is_recurring=1
  AND YEAR(p.[date])=@previousYear AND MONTH(p.[date])=@previousMonth
  AND NOT EXISTS (SELECT 1 FROM dbo.monthly_income_entries c WHERE YEAR(c.[date])=@year AND MONTH(c.[date])=@month AND LOWER(LTRIM(RTRIM(c.[name])))=LOWER(LTRIM(RTRIM(p.[name]))));
SELECT @@ROWCOUNT;", con);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@date", new DateTime(year, month, Math.Min(DateTime.Today.Day, DateTime.DaysInMonth(year, month))));
            cmd.Parameters.AddWithValue("@previousYear", previous.Year);
            cmd.Parameters.AddWithValue("@previousMonth", previous.Month);
            cmd.Parameters.AddWithValue("@year", year);
            cmd.Parameters.AddWithValue("@month", month);
            added += Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        await SyncMonthlyIncomeTotalAsync(year, month);
        return added;
    }

    private async Task SyncMonthlyIncomeTotalAsync(int year, int month)
    {
        var total = await GetMonthlyIncomeEntriesTotalAsync(year, month) ?? 0m;
        await SaveIncomeAsync(year, month, total, 0);
    }

    public async Task<List<AccountBalance>> GetAccountsAsync(decimal emergencyFund)
    {
        await EnsureModernTablesAsync();
        var accounts = new List<AccountBalance>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT account_balance_id,[name],amount,interest_rate,monthly_contribution,include_in_global_goal,updated_at,include_in_savings_command,starting_balance,provider,account_type,holding_type,tax_treatment,tax_rate,tax_effective_from,interest_handling,purpose,is_default_emergency_fund_destination,usage_type,last_four_digits,statement_parser,is_active
FROM dbo.account_balances ORDER BY [name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            accounts.Add(new AccountBalance(
                r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4),
                r.GetBoolean(5), r.GetDateTime(6), r.GetBoolean(7), r.GetDecimal(8), r.GetString(9),
                r.GetString(10), r.GetString(11), r.GetString(12), r.GetDecimal(13),
                r.IsDBNull(14) ? null : r.GetDateTime(14), r.GetString(15), r.GetString(16), r.GetBoolean(17),
                r.GetString(18), r.IsDBNull(19) ? null : r.GetString(19), r.GetString(20), r.GetBoolean(21)));
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


    public async Task<(long TransactionId, decimal PreviousBalance, decimal NewBalance, decimal Baseline, decimal RemainingShortfall)> AddEmergencyFundContributionAsync(decimal amount, string? note)
    {
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Contribution must be greater than zero.");
        await EnsureModernTablesAsync();

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        try
        {
            int destinationAccountId;
            string destinationAccountName;
            await using (var readDestination = new SqlCommand(@"SELECT TOP 1 account_balance_id,[name],amount
FROM dbo.account_balances WITH (UPDLOCK,HOLDLOCK)
WHERE purpose='EmergencyFund'
ORDER BY is_default_emergency_fund_destination DESC,account_balance_id", con, tx))
            {
                await using var destinationReader = await readDestination.ExecuteReaderAsync();
                if (!await destinationReader.ReadAsync())
                    throw new InvalidOperationException("Choose an account purpose of Emergency fund before recording a contribution.");
                destinationAccountId = destinationReader.GetInt32(0);
                destinationAccountName = destinationReader.GetString(1);
            }

            decimal previous;
            await using (var readBalance = new SqlCommand("SELECT COALESCE(SUM(amount),0) FROM dbo.account_balances WHERE purpose='EmergencyFund'", con, tx))
                previous = Convert.ToDecimal(await readBalance.ExecuteScalarAsync() ?? 0m);

            var updated = previous + amount;
            await using (var updateBalance = new SqlCommand("UPDATE dbo.account_balances SET amount=amount+@amount,updated_at=SYSUTCDATETIME() WHERE account_balance_id=@id", con, tx))
            {
                updateBalance.Parameters.AddWithValue("@amount", amount);
                updateBalance.Parameters.AddWithValue("@id", destinationAccountId);
                await updateBalance.ExecuteNonQueryAsync();
            }

            decimal baseline;
            await using (var readBaseline = new SqlCommand(
                "SELECT TRY_CONVERT(decimal(18,2),[value]) FROM dbo.finance_settings WHERE [key]='EmergencyFundBaseline'", con, tx))
            {
                var baselineValue = await readBaseline.ExecuteScalarAsync();
                baseline = baselineValue is null or DBNull ? 12000m : Convert.ToDecimal(baselineValue);
            }

            var shortfall = Math.Max(0m, baseline - updated);
            var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            var description = cleanNote is null
                ? $"Emergency Fund increased from {previous:C} to {updated:C}. Remaining shortfall: {shortfall:C}."
                : $"{cleanNote} Emergency Fund increased from {previous:C} to {updated:C}. Remaining shortfall: {shortfall:C}.";

            int savingsId;
            await using (var addDashboardAllocation = new SqlCommand(@"INSERT INTO dbo.savings
([name],amount,[date],[type],[length],notes,pot_name_snapshot,reserve_pot_id)
OUTPUT INSERTED.savings_id
VALUES(@accountName,@amount,CONVERT(date,GETDATE()),'Emergency Fund Restoration','One-off',@notes,@accountName,NULL)", con, tx))
            {
                addDashboardAllocation.Parameters.AddWithValue("@amount", amount);
                addDashboardAllocation.Parameters.AddWithValue("@accountName", destinationAccountName);
                addDashboardAllocation.Parameters.AddWithValue("@notes", cleanNote is null
                    ? "Added through Account Management emergency-fund restoration."
                    : $"Added through Account Management emergency-fund restoration. {cleanNote}");
                savingsId = Convert.ToInt32(await addDashboardAllocation.ExecuteScalarAsync());
            }

            long eventId;
            await using (var addEvent = new SqlCommand(@"INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source)
OUTPUT INSERTED.finance_event_id
VALUES('Household Reserve','EmergencyFundContribution','Account',@accountId,@title,@description,@amount,'User')", con, tx))
            {
                addEvent.Parameters.AddWithValue("@description", description);
                addEvent.Parameters.AddWithValue("@amount", amount);
                addEvent.Parameters.AddWithValue("@accountId", destinationAccountId);
                addEvent.Parameters.AddWithValue("@title", $"{destinationAccountName} contribution");
                eventId = Convert.ToInt64(await addEvent.ExecuteScalarAsync());
            }

            long transactionId;
            await using (var addTransaction = new SqlCommand(@"INSERT INTO dbo.emergency_fund_transactions
(transaction_type,amount,note,dashboard_savings_id,finance_event_id,account_balance_id)
OUTPUT INSERTED.emergency_fund_transaction_id
VALUES('Contribution',@amount,@note,@savingsId,@eventId,@accountId)", con, tx))
            {
                addTransaction.Parameters.AddWithValue("@amount", amount);
                addTransaction.Parameters.AddWithValue("@note", (object?)cleanNote ?? DBNull.Value);
                addTransaction.Parameters.AddWithValue("@savingsId", savingsId);
                addTransaction.Parameters.AddWithValue("@eventId", eventId);
                addTransaction.Parameters.AddWithValue("@accountId", destinationAccountId);
                transactionId = Convert.ToInt64(await addTransaction.ExecuteScalarAsync());
            }

            await tx.CommitAsync();
            return (transactionId, previous, updated, baseline, shortfall);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(decimal PreviousBalance, decimal NewBalance, decimal Baseline, decimal RemainingShortfall)> ReverseEmergencyFundTransactionAsync(long transactionId, string? reason)
    {
        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        try
        {
            decimal amount;
            int? savingsId;
            int accountId;
            string accountName;
            await using (var read = new SqlCommand(@"SELECT t.amount,t.dashboard_savings_id,t.note,t.reversed_by_transaction_id,t.transaction_type,t.account_balance_id,a.[name]
FROM dbo.emergency_fund_transactions t
LEFT JOIN dbo.account_balances a ON a.account_balance_id=t.account_balance_id
WHERE t.emergency_fund_transaction_id=@id", con, tx))
            {
                read.Parameters.AddWithValue("@id", transactionId);
                await using var r = await read.ExecuteReaderAsync();
                if (!await r.ReadAsync()) throw new InvalidOperationException("The selected emergency-fund transaction could not be found.");
                if (!string.Equals(r.GetString(4), "Contribution", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Only contribution transactions can currently be reversed.");
                if (!r.IsDBNull(3)) throw new InvalidOperationException("This contribution has already been reversed.");
                amount = r.GetDecimal(0);
                savingsId = r.IsDBNull(1) ? null : r.GetInt32(1);
                if (r.IsDBNull(5) || r.IsDBNull(6)) throw new InvalidOperationException("This legacy contribution is not linked to an account and cannot be reversed automatically.");
                accountId = r.GetInt32(5);
                accountName = r.GetString(6);
            }

            decimal previous;
            await using (var readBalance = new SqlCommand("SELECT COALESCE(SUM(amount),0) FROM dbo.account_balances WHERE purpose='EmergencyFund'", con, tx))
                previous = Convert.ToDecimal(await readBalance.ExecuteScalarAsync() ?? 0m);
            decimal accountBalance;
            await using (var readAccountBalance = new SqlCommand("SELECT amount FROM dbo.account_balances WITH (UPDLOCK,HOLDLOCK) WHERE account_balance_id=@id", con, tx))
            {
                readAccountBalance.Parameters.AddWithValue("@id", accountId);
                accountBalance = Convert.ToDecimal(await readAccountBalance.ExecuteScalarAsync() ?? 0m);
            }
            if (accountBalance < amount) throw new InvalidOperationException($"This contribution cannot be reversed because {accountName} no longer contains the full contribution amount.");
            var updated = previous - amount;
            await using (var updateBalance = new SqlCommand("UPDATE dbo.account_balances SET amount=amount-@amount,updated_at=SYSUTCDATETIME() WHERE account_balance_id=@id", con, tx))
            {
                updateBalance.Parameters.AddWithValue("@amount", amount);
                updateBalance.Parameters.AddWithValue("@id", accountId);
                await updateBalance.ExecuteNonQueryAsync();
            }

            if (savingsId.HasValue)
            {
                await using (var unlinkSaving = new SqlCommand("UPDATE dbo.emergency_fund_transactions SET dashboard_savings_id=NULL WHERE emergency_fund_transaction_id=@transactionId", con, tx))
                {
                    unlinkSaving.Parameters.AddWithValue("@transactionId", transactionId);
                    await unlinkSaving.ExecuteNonQueryAsync();
                }
                await using var deleteSaving = new SqlCommand("DELETE FROM dbo.savings WHERE savings_id=@id", con, tx);
                deleteSaving.Parameters.AddWithValue("@id", savingsId.Value);
                await deleteSaving.ExecuteNonQueryAsync();
            }

            decimal baseline;
            await using (var readBaseline = new SqlCommand("SELECT TRY_CONVERT(decimal(18,2),[value]) FROM dbo.finance_settings WHERE [key]='EmergencyFundBaseline'", con, tx))
            {
                var value = await readBaseline.ExecuteScalarAsync();
                baseline = value is null or DBNull ? 12000m : Convert.ToDecimal(value);
            }
            var shortfall = Math.Max(0m, baseline - updated);
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Contribution reversed by user." : reason.Trim();
            var description = $"Reversed emergency-fund contribution #{transactionId} of {amount:C}. Balance changed from {previous:C} to {updated:C}. Remaining shortfall: {shortfall:C}. Reason: {cleanReason}";

            long eventId;
            await using (var addEvent = new SqlCommand(@"INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source)
OUTPUT INSERTED.finance_event_id
VALUES('Household Reserve','EmergencyFundContributionReversed','Account',@accountId,@title,@description,@amount,'User')", con, tx))
            {
                addEvent.Parameters.AddWithValue("@description", description);
                addEvent.Parameters.AddWithValue("@amount", -amount);
                addEvent.Parameters.AddWithValue("@accountId", accountId);
                addEvent.Parameters.AddWithValue("@title", $"{accountName} contribution reversed");
                eventId = Convert.ToInt64(await addEvent.ExecuteScalarAsync());
            }

            long reversalId;
            await using (var addReversal = new SqlCommand(@"INSERT INTO dbo.emergency_fund_transactions
(transaction_type,amount,note,finance_event_id,reversed_transaction_id,account_balance_id)
OUTPUT INSERTED.emergency_fund_transaction_id
VALUES('Reversal',@amount,@note,@eventId,@originalId,@accountId)", con, tx))
            {
                addReversal.Parameters.AddWithValue("@amount", -amount);
                addReversal.Parameters.AddWithValue("@note", cleanReason);
                addReversal.Parameters.AddWithValue("@eventId", eventId);
                addReversal.Parameters.AddWithValue("@originalId", transactionId);
                addReversal.Parameters.AddWithValue("@accountId", accountId);
                reversalId = Convert.ToInt64(await addReversal.ExecuteScalarAsync());
            }

            await using (var markOriginal = new SqlCommand("UPDATE dbo.emergency_fund_transactions SET reversed_by_transaction_id=@reversalId WHERE emergency_fund_transaction_id=@id", con, tx))
            {
                markOriginal.Parameters.AddWithValue("@reversalId", reversalId);
                markOriginal.Parameters.AddWithValue("@id", transactionId);
                await markOriginal.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return (previous, updated, baseline, shortfall);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<List<EmergencyFundTransactionRow>> GetEmergencyFundTransactionsAsync(SqlConnection con)
    {
        var rows = new List<EmergencyFundTransactionRow>();
        await using var cmd = new SqlCommand(@"SELECT emergency_fund_transaction_id,transaction_type,amount,occurred_at,note,reversed_transaction_id,reversed_by_transaction_id
FROM dbo.emergency_fund_transactions
ORDER BY occurred_at DESC,emergency_fund_transaction_id DESC", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new EmergencyFundTransactionRow(r.GetInt64(0),r.GetString(1),r.GetDecimal(2),r.GetDateTime(3),r.IsDBNull(4)?null:r.GetString(4),r.IsDBNull(5)?null:r.GetInt64(5),r.IsDBNull(6)?null:r.GetInt64(6)));
        }
        return rows;
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

    public async Task SaveAccountAsync(
        int id,
        string name,
        decimal amount,
        decimal rate,
        decimal monthly,
        bool include,
        decimal startingBalance,
        string provider,
        string accountType,
        string holdingType,
        string taxTreatment,
        decimal taxRate,
        DateTime? taxEffectiveFrom,
        string interestHandling,
        string purpose,
        bool isDefaultEmergencyFundDestination,
        string usageType = "Tracking",
        string? lastFourDigits = null,
        string statementParser = "Generic",
        bool isActive = true)
    {
        await EnsureModernTablesAsync();
        provider = string.IsNullOrWhiteSpace(provider) ? "Other" : provider.Trim();
        accountType = string.IsNullOrWhiteSpace(accountType) ? "Savings" : accountType.Trim();
        holdingType = string.IsNullOrWhiteSpace(holdingType) ? "Cash" : holdingType.Trim();
        taxTreatment = string.IsNullOrWhiteSpace(taxTreatment) ? "Tax Free" : taxTreatment.Trim();
        taxRate = taxTreatment.Equals("Taxable", StringComparison.OrdinalIgnoreCase) ? Math.Clamp(taxRate, 0m, 100m) : 0m;
        taxEffectiveFrom = taxTreatment.Equals("Taxable", StringComparison.OrdinalIgnoreCase) ? taxEffectiveFrom?.Date : null;
        interestHandling = NormalizeInterestHandling(interestHandling);
        purpose = NormalizeAccountPurpose(purpose);
        usageType = NormalizeAccountUsageType(usageType);
        lastFourDigits = NormalizeLastFourDigits(lastFourDigits);
        statementParser = string.IsNullOrWhiteSpace(statementParser) ? "Generic" : statementParser.Trim();
        if (usageType == "StatementOnly")
        {
            include = false;
            isDefaultEmergencyFundDestination = false;
        }
        isDefaultEmergencyFundDestination = usageType != "StatementOnly" && purpose == "EmergencyFund" && isDefaultEmergencyFundDestination;

        if (id == 0)
        {
            await ExecuteAsync(@"INSERT INTO dbo.account_balances([name],amount,interest_rate,monthly_contribution,include_in_global_goal,starting_balance,provider,account_type,holding_type,tax_treatment,tax_rate,tax_effective_from,interest_handling,purpose,is_default_emergency_fund_destination,usage_type,last_four_digits,statement_parser,is_active)
VALUES(@name,@amount,@rate,@monthly,@include,@starting,@provider,@accountType,@holdingType,@taxTreatment,@taxRate,@taxEffectiveFrom,@interestHandling,@purpose,@isDefault,@usageType,@lastFourDigits,@statementParser,@isActive)",
                ("@name", name), ("@amount", amount), ("@rate", rate), ("@monthly", monthly), ("@include", include),
                ("@starting", startingBalance), ("@provider", provider), ("@accountType", accountType), ("@holdingType", holdingType),
                ("@taxTreatment", taxTreatment), ("@taxRate", taxRate), ("@taxEffectiveFrom", taxEffectiveFrom.HasValue ? taxEffectiveFrom.Value : DBNull.Value), ("@interestHandling", interestHandling), ("@purpose", purpose), ("@isDefault", isDefaultEmergencyFundDestination), ("@usageType", usageType), ("@lastFourDigits", DbValue(lastFourDigits)), ("@statementParser", statementParser), ("@isActive", isActive));
            id = Convert.ToInt32(await ScalarAsync("SELECT TOP 1 account_balance_id FROM dbo.account_balances WHERE [name]=@name ORDER BY account_balance_id DESC", ("@name", name)));
        }
        else
        {
            await ExecuteAsync(@"UPDATE dbo.account_balances SET [name]=@name,amount=@amount,interest_rate=@rate,monthly_contribution=@monthly,include_in_global_goal=@include,starting_balance=@starting,provider=@provider,account_type=@accountType,holding_type=@holdingType,tax_treatment=@taxTreatment,tax_rate=@taxRate,tax_effective_from=@taxEffectiveFrom,interest_handling=@interestHandling,purpose=@purpose,is_default_emergency_fund_destination=@isDefault,usage_type=@usageType,last_four_digits=@lastFourDigits,statement_parser=@statementParser,is_active=@isActive,updated_at=SYSUTCDATETIME() WHERE account_balance_id=@id",
                ("@id", id), ("@name", name), ("@amount", amount), ("@rate", rate), ("@monthly", monthly), ("@include", include),
                ("@starting", startingBalance), ("@provider", provider), ("@accountType", accountType), ("@holdingType", holdingType),
                ("@taxTreatment", taxTreatment), ("@taxRate", taxRate), ("@taxEffectiveFrom", taxEffectiveFrom.HasValue ? taxEffectiveFrom.Value : DBNull.Value), ("@interestHandling", interestHandling), ("@purpose", purpose), ("@isDefault", isDefaultEmergencyFundDestination), ("@usageType", usageType), ("@lastFourDigits", DbValue(lastFourDigits)), ("@statementParser", statementParser), ("@isActive", isActive));
        }

        if (isDefaultEmergencyFundDestination)
            await ExecuteAsync("UPDATE dbo.account_balances SET is_default_emergency_fund_destination=CASE WHEN account_balance_id=@id THEN 1 ELSE 0 END WHERE purpose='EmergencyFund'", ("@id", id));
        else if (purpose != "EmergencyFund")
            await ExecuteAsync("UPDATE dbo.account_balances SET is_default_emergency_fund_destination=0 WHERE account_balance_id=@id", ("@id", id));

        await ExecuteAsync("IF EXISTS (SELECT 1 FROM dbo.account_balances WHERE purpose='EmergencyFund') AND NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE purpose='EmergencyFund' AND is_default_emergency_fund_destination=1) UPDATE dbo.account_balances SET is_default_emergency_fund_destination=1 WHERE account_balance_id=(SELECT TOP 1 account_balance_id FROM dbo.account_balances WHERE purpose='EmergencyFund' ORDER BY account_balance_id)");

        await ExecuteAsync("INSERT INTO dbo.account_balance_history(account_balance_id,[name],amount,interest_rate,monthly_contribution) VALUES(@id,@name,@amount,@rate,@monthly)",
            ("@id", id), ("@name", name), ("@amount", amount), ("@rate", rate), ("@monthly", monthly));
    }





    private static string NormalizeAccountUsageType(string? usageType)
        => usageType switch
        {
            "StatementOnly" => "StatementOnly",
            "Both" => "Both",
            _ => "Tracking"
        };

    private static string? NormalizeLastFourDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? digits : digits[^4..];
    }

    private static string NormalizeAccountPurpose(string? purpose)
        => purpose switch
        {
            "EmergencyFund" => "EmergencyFund",
            "Bills" => "Bills",
            "EverydaySpending" => "EverydaySpending",
            "Savings" => "Savings",
            "Investment" => "Investment",
            _ => "General"
        };

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

    public async Task AddPaymentAsync(string source, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes, int? accountBalanceId = null)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        switch (source)
        {
            case "bills":
                await ExecuteAsync("INSERT INTO dbo.bills([name], amount, [date], [type], [length], [description], account_balance_id) VALUES(@name,@amount,@date,@type,@length,@notes,@accountBalanceId)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "everyday_spending":
                await ExecuteAsync("INSERT INTO dbo.everyday_spending([name], amount, [date], category, [type], [length], [description], account_balance_id) VALUES(@name,@amount,@date,@category,@type,@length,@notes,@accountBalanceId)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "extra_expenses":
                await ExecuteAsync("INSERT INTO dbo.extra_expenses([name], amount, duedate, category, [type], [length], [description], account_balance_id) VALUES(@name,@amount,@date,@category,@type,@length,@notes,@accountBalanceId)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "investments":
                await ExecuteAsync("INSERT INTO dbo.investments([name], amount, [date], category, [type], [length], notes, account_balance_id) VALUES(@name,@amount,@date,@category,@type,@length,@notes,@accountBalanceId)",
                    ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "savings":
                await ExecuteAsync("INSERT INTO dbo.savings([name], amount, [date], [type], [length], notes, pot_name_snapshot, account_balance_id) VALUES(@name,@amount,@date,@type,@length,@notes,@snapshot,@accountBalanceId)",
                    ("@name", name.Trim()), ("@amount", amount), ("@date", date), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@snapshot", name.Trim()), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
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
        INSERT INTO dbo.bills([name], amount, [date], [type], [length], [description], account_balance_id)
        SELECT [name], amount, @toDate, [type],
               CASE
                   WHEN TRY_CONVERT(int, [length]) IS NOT NULL AND TRY_CONVERT(int, [length]) > 1
                       THEN CONVERT(nvarchar(50), TRY_CONVERT(int, [length]) - 1)
                   ELSE [length]
               END,
               @autoNote, account_balance_id
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
        INSERT INTO dbo.investments([name], amount, [date], category, [length], notes, account_balance_id)
        SELECT [name], amount, @toDate, category,
               CASE
                   WHEN TRY_CONVERT(int, [length]) IS NOT NULL AND TRY_CONVERT(int, [length]) > 1
                       THEN CONVERT(nvarchar(50), TRY_CONVERT(int, [length]) - 1)
                   ELSE [length]
               END,
               @autoNote, account_balance_id
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

        var monthlyIncome = await GetMonthlyIncomeEntriesTotalAsync(year, month) ?? 0m;

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

        // V2 carries the actual monthly result. There is no fixed monthly savings target.
        var carryAmount = Math.Round(remainingFund, 2);

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

        var monthlyIncome = await GetMonthlyIncomeEntriesTotalAsync(year, month) ?? 0m;
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

    public async Task<decimal> CarryMonthResultForwardAsync(int year, int month)
    {
        var next = new DateTime(year, month, 1).AddMonths(1);
        var monthResult = await GetMonthResultAsync(year, month);
        await SaveCarryForwardAsync(next.Year, next.Month, monthResult, year, month);
        return monthResult;
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

    private async Task<decimal> GetForecastContributionPaceAsync(string potName, decimal fallback)
    {
        var method = await GetStringSettingAsync("ForecastMethod", "Last3Months");
        var take = method switch
        {
            "LatestMonth" => 1,
            "Last6Months" => 6,
            _ => 3
        };

        var value = await ScalarAsync("""
SELECT AVG(month_total)
FROM (
    SELECT TOP (@take) SUM(amount) AS month_total, YEAR([date]) AS [year], MONTH([date]) AS [month]
    FROM dbo.savings
    WHERE [name] = @name AND amount > 0
    GROUP BY YEAR([date]), MONTH([date])
    ORDER BY YEAR([date]) DESC, MONTH([date]) DESC
) recent_months
""", ("@take", take), ("@name", potName));

        return value is null or DBNull ? Math.Max(0m, fallback) : Math.Max(0m, Convert.ToDecimal(value));
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
        await EnsureModernTablesAsync();
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Id: "billid", Date: "[date]", Category: "NULL", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
            "everyday_spending" => (Table: "dbo.everyday_spending", Id: "everyday_spending_id", Date: "[date]", Category: "category", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
            "extra_expenses" => (Table: "dbo.extra_expenses", Id: "extra_expense_id", Date: "duedate", Category: "category", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
            "investments" => (Table: "dbo.investments", Id: "investments_id", Date: "[date]", Category: "category", Type: "NULL", Length: "length", Notes: "notes", IdParam: "@id"),
            "savings" => (Table: "dbo.savings", Id: "savings_id", Date: "[date]", Category: "NULL", Type: "NULL", Length: "length", Notes: "notes", IdParam: "@id"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        var reservePotId = source == "savings" ? "p.reserve_pot_id" : "NULL";
        var potNameSnapshot = source == "savings" ? "p.pot_name_snapshot" : "NULL";
        var currentReservePotName = source == "savings" ? "rp.[name]" : "NULL";
        var tableExpression = source == "savings"
            ? $"{map.Table} p LEFT JOIN dbo.reserve_pots rp ON rp.reserve_pot_id = p.reserve_pot_id LEFT JOIN dbo.account_balances ab ON ab.account_balance_id = p.account_balance_id"
            : $"{map.Table} p LEFT JOIN dbo.account_balances ab ON ab.account_balance_id = p.account_balance_id";

        var sql = $"SELECT p.{map.Id} AS id, p.[name], p.amount, p.{map.Date} AS [date], {map.Category} AS category, {map.Type} AS [type], p.{map.Length} AS [length], p.{map.Notes} AS notes, p.account_balance_id, ab.[name], {reservePotId}, {potNameSnapshot}, {currentReservePotName} FROM {tableExpression} WHERE p.{map.Id}=@id";
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            return new PaymentRow(
                r.GetInt32(0),
                r.GetString(1),
                r.GetDecimal(2),
                r.GetDateTime(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                source,
                r.IsDBNull(8) ? null : r.GetInt32(8),
                r.IsDBNull(9) ? null : r.GetString(9),
                r.IsDBNull(10) ? null : r.GetInt32(10),
                r.IsDBNull(11) ? null : r.GetString(11),
                r.IsDBNull(12) ? null : r.GetString(12));
        }
        return null;
    }

    public async Task UpdatePaymentAsync(string source, int id, string name, decimal amount, DateTime date, string? category, string? type, string? length, string? notes, int? accountBalanceId = null)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        switch (source)
        {
            case "bills":
                await ExecuteAsync("UPDATE dbo.bills SET [name]=@name, amount=@amount, [date]=@date, [type]=@type, [length]=@length, [description]=@notes,account_balance_id=@accountBalanceId WHERE billid=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "everyday_spending":
                await ExecuteAsync("UPDATE dbo.everyday_spending SET [name]=@name, amount=@amount, [date]=@date, category=@category, [type]=@type, [length]=@length, [description]=@notes,account_balance_id=@accountBalanceId WHERE everyday_spending_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "extra_expenses":
                await ExecuteAsync("UPDATE dbo.extra_expenses SET [name]=@name, amount=@amount, duedate=@date, category=@category, [type]=@type, [length]=@length, [description]=@notes,account_balance_id=@accountBalanceId WHERE extra_expense_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "investments":
                await ExecuteAsync("UPDATE dbo.investments SET [name]=@name, amount=@amount, [date]=@date, category=@category, [type]=@type, [length]=@length, notes=@notes,account_balance_id=@accountBalanceId WHERE investments_id=@id", ("@id", id), ("@name", name), ("@amount", amount), ("@date", date), ("@category", DbValue(category)), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
                break;
            case "savings":
                await ExecuteAsync("UPDATE dbo.savings SET [name]=@name, amount=@amount, [date]=@date, [type]=@type, [length]=@length, notes=@notes, pot_name_snapshot=COALESCE(pot_name_snapshot,@snapshot),account_balance_id=@accountBalanceId WHERE savings_id=@id", ("@id", id), ("@name", name.Trim()), ("@amount", amount), ("@date", date), ("@type", DbValue(type)), ("@length", DbValue(length)), ("@notes", DbValue(notes)), ("@snapshot", name.Trim()), ("@accountBalanceId", accountBalanceId ?? (object)DBNull.Value));
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
    public async Task<string> GetStringSettingAsync(string key, string fallback)
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync("SELECT [value] FROM dbo.finance_settings WHERE [key]=@key", ("@key", key));
        return value is null or DBNull ? config[$"FinanceSettings:{key}"] ?? fallback : Convert.ToString(value) ?? fallback;
    }

    public async Task SaveStringSettingAsync(string key, string value)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("MERGE dbo.finance_settings AS t USING (SELECT @key AS [key]) AS s ON t.[key]=s.[key] WHEN MATCHED THEN UPDATE SET [value]=@value, updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([key],[value]) VALUES(@key,@value);", ("@key", key), ("@value", value));
    }

    public async Task<bool> GetBoolSettingAsync(string key, bool fallback)
        => bool.TryParse(await GetStringSettingAsync(key, fallback.ToString()), out var value) ? value : fallback;

    public Task SaveBoolSettingAsync(string key, bool value)
        => SaveStringSettingAsync(key, value.ToString());

    public async Task<int> GetIntSettingAsync(string key, int fallback)
        => int.TryParse(await GetStringSettingAsync(key, fallback.ToString()), out var value) ? value : fallback;

    public Task SaveIntSettingAsync(string key, int value)
        => SaveStringSettingAsync(key, value.ToString());
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

    public async Task<ReservePot?> GetReservePotByIdAsync(int reservePotId)
    {
        if (reservePotId <= 0) return null;
        var pots = await GetReservePotsAsync();
        return pots.FirstOrDefault(x => x.Id == reservePotId);
    }

    public async Task<IReadOnlyList<ReservePotPickerItem>> GetSelectableReservePotsAsync()
    {
        await EnsureModernTablesAsync();
        var items = new List<ReservePotPickerItem>();

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT reserve_pot_id,[name],allocated_amount,target_amount,is_active FROM dbo.reserve_pots WHERE is_active=1 ORDER BY priority,[name]", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ReservePotPickerItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                reader.GetBoolean(4)));
        }

        return items;
    }

    public async Task<ReservePot?> FindReservePotByNormalisedNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalisedName = name.Trim();
        var pots = await GetReservePotsAsync();
        return pots.FirstOrDefault(x => string.Equals(x.Name.Trim(), normalisedName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<ReservePot>> GetReservePotsAsync()
    {
        await EnsureModernTablesAsync();
        var list = new List<ReservePot>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT reserve_pot_id,[name],allocated_amount,default_monthly_contribution,intended_monthly_contribution,funding_frequency,expected_funding_day,carry_forward_shortfalls,carry_excess_forward,funding_paused_from,funding_paused_until,funding_pause_reason,funding_plan_start_date,target_amount,due_date,priority,is_active,notes,starting_amount,updated_at FROM dbo.reserve_pots ORDER BY priority,[name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ReservePot(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetInt32(6), r.GetBoolean(7), r.GetBoolean(8), r.IsDBNull(9) ? null : r.GetDateTime(9), r.IsDBNull(10) ? null : r.GetDateTime(10), r.IsDBNull(11) ? null : r.GetString(11), r.GetDateTime(12), r.IsDBNull(13) ? null : r.GetDecimal(13), r.IsDBNull(14) ? null : r.GetDateTime(14), r.GetInt32(15), r.GetBoolean(16), r.IsDBNull(17) ? null : r.GetString(17), r.GetDecimal(18), r.GetDateTime(19)));
        return list;
    }

    public async Task<int> SaveReservePotAsync(int id, string name, decimal allocatedAmount, decimal startingAmount, decimal monthlyContribution, decimal intendedMonthlyContribution, string fundingFrequency, int? expectedFundingDay, bool carryForwardShortfalls, bool carryExcessForward, DateTime? fundingPausedFrom, DateTime? fundingPausedUntil, string? fundingPauseReason, DateTime? fundingPlanStartDate, decimal? targetAmount, DateTime? dueDate, int priority, bool isActive, string? notes)
    {
        await EnsureModernTablesAsync();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pot name is required.", nameof(name));

        // Frequency and expected funding day are retained in the schema for backwards
        // compatibility, but V2 now uses flexible monthly funding.
        const string normalisedFrequency = "Monthly";
        expectedFundingDay = null;

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
            ("@starting", Math.Max(0, startingAmount)),
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
            await ExecuteAsync(@"INSERT INTO dbo.reserve_pots([name],allocated_amount,starting_amount,default_monthly_contribution,intended_monthly_contribution,funding_frequency,expected_funding_day,carry_forward_shortfalls,carry_excess_forward,funding_paused_from,funding_paused_until,funding_pause_reason,funding_plan_start_date,target_amount,due_date,priority,is_active,notes)
VALUES(@name,@allocated,@starting,@monthly,@intended,@frequency,@fundingDay,@carryForward,@carryExcess,@pausedFrom,@pausedUntil,@pauseReason,@planStart,@target,@due,@priority,@active,@notes)", parameters);
            id = Convert.ToInt32(await ScalarAsync("SELECT TOP 1 reserve_pot_id FROM dbo.reserve_pots WHERE [name]=@name ORDER BY reserve_pot_id DESC", ("@name", name.Trim())));
            await AddFinanceEventAsync("Household Reserve", "PotCreated", "ReservePot", id, $"{name.Trim()} created", "A new virtual allocation was created.", allocatedAmount, "User");
        }
        else
        {
            await ExecuteAsync(@"UPDATE dbo.reserve_pots SET [name]=@name,allocated_amount=@allocated,starting_amount=@starting,default_monthly_contribution=@monthly,intended_monthly_contribution=@intended,funding_frequency=@frequency,expected_funding_day=@fundingDay,carry_forward_shortfalls=@carryForward,carry_excess_forward=@carryExcess,funding_paused_from=@pausedFrom,funding_paused_until=@pausedUntil,funding_pause_reason=@pauseReason,funding_plan_start_date=@planStart,target_amount=@target,due_date=@due,priority=@priority,is_active=@active,notes=@notes,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@id", parameters.Append(("@id", (object)id)).ToArray());
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
        DateTime? estimatedCompletion = null;
        var recentMonthlyContribution = await GetForecastContributionPaceAsync(pot.Name, pot.DefaultMonthlyContribution);
        if (pot.TargetAmount.HasValue && pot.TargetAmount.Value > pot.AllocatedAmount && recentMonthlyContribution > 0m)
        {
            var monthsToTarget = (int)Math.Ceiling((pot.TargetAmount.Value - pot.AllocatedAmount) / recentMonthlyContribution);
            estimatedCompletion = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(Math.Max(1, monthsToTarget));
        }
        else if (pot.TargetAmount.HasValue && pot.AllocatedAmount >= pot.TargetAmount.Value)
        {
            estimatedCompletion = DateTime.Today;
        }

        if (pot.TargetAmount.HasValue && pot.DueDate.HasValue && pot.DueDate.Value.Date >= DateTime.Today)
        {
            var monthsRemaining = Math.Max(1, ((pot.DueDate.Value.Year - DateTime.Today.Year) * 12) + pot.DueDate.Value.Month - DateTime.Today.Month + 1);
            requiredMonthly = Math.Round(Math.Max(0m, pot.TargetAmount.Value - pot.AllocatedAmount) / monthsRemaining, 2);
            projected = Math.Round(pot.AllocatedAmount + recentMonthlyContribution * monthsRemaining, 2);
            projectedShortfall = Math.Max(0m, pot.TargetAmount.Value - projected);
            extraRequired = Math.Max(0m, requiredMonthly.Value - recentMonthlyContribution);
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
            RecentMonthlyContribution = recentMonthlyContribution,
            EstimatedCompletionDate = estimatedCompletion,
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

    public async Task<List<FinanceEventRow>> GetFinanceEventsAsync(int? potId = null, string? eventType = null, string? area = null, bool includeDetailedAudit = false, DateTime? from = null, DateTime? to = null)
    {
        await EnsureModernTablesAsync();
        var list = new List<FinanceEventRow>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        var sql = "SELECT finance_event_id,occurred_at,area,event_type,entity_type,entity_id,title,[description],amount,source FROM dbo.finance_events WHERE (@potId IS NULL OR (entity_type='ReservePot' AND entity_id=@potId)) AND (@eventType IS NULL OR event_type=@eventType) AND (@area IS NULL OR area=@area) AND (@includeDetailed=1 OR event_type<>'AuditAction') AND (@from IS NULL OR occurred_at>=@from) AND (@to IS NULL OR occurred_at<DATEADD(day,1,@to)) ORDER BY occurred_at DESC";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@potId", potId.HasValue ? potId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@eventType", string.IsNullOrWhiteSpace(eventType) ? DBNull.Value : eventType);
        cmd.Parameters.AddWithValue("@area", string.IsNullOrWhiteSpace(area) ? DBNull.Value : area);
        cmd.Parameters.AddWithValue("@includeDetailed", includeDetailedAudit);
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


    public async Task<Dictionary<int, List<ReservePotInvestmentStage>>> GetReservePotInvestmentStagesAsync()
    {
        await EnsureModernTablesAsync();
        var result = new Dictionary<int, List<ReservePotInvestmentStage>>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT reserve_pot_investment_stage_id,reserve_pot_id,stage_order,investment_type,provider,expected_annual_return,start_date,end_date,notes,updated_at
FROM dbo.reserve_pot_investment_stages ORDER BY reserve_pot_id,stage_order", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var stage = new ReservePotInvestmentStage(r.GetInt32(0),r.GetInt32(1),r.GetInt32(2),r.GetString(3),r.GetString(4),r.GetDecimal(5),r.GetDateTime(6),r.IsDBNull(7)?null:r.GetDateTime(7),r.IsDBNull(8)?null:r.GetString(8),r.GetDateTime(9));
            if (!result.TryGetValue(stage.ReservePotId, out var list)) result[stage.ReservePotId] = list = [];
            list.Add(stage);
        }
        return result;
    }

    public async Task SaveReservePotInvestmentStageAsync(int id, int potId, int stageOrder, string investmentType, string provider, decimal expectedAnnualReturn, DateTime startDate, DateTime? endDate, string? notes)
    {
        await EnsureModernTablesAsync();
        if (potId <= 0) throw new ArgumentException("A Money Pot is required.");
        if (string.IsNullOrWhiteSpace(investmentType)) throw new ArgumentException("Investment type is required.");
        if (endDate.HasValue && endDate.Value.Date <= startDate.Date) throw new ArgumentException("The stage end date must be after its start date.");
        if (expectedAnnualReturn < -100m || expectedAnnualReturn > 100m) throw new ArgumentException("Expected annual return must be between -100% and 100%.");
        var duplicate = await ScalarAsync("SELECT COUNT(*) FROM dbo.reserve_pot_investment_stages WHERE reserve_pot_id=@potId AND stage_order=@stageOrder AND reserve_pot_investment_stage_id<>@id", ("@potId",potId),("@stageOrder",Math.Max(1,stageOrder)),("@id",id));
        if (Convert.ToInt32(duplicate) > 0) throw new ArgumentException("That stage order is already in use for this pot.");
        if (id <= 0)
            await ExecuteAsync(@"INSERT INTO dbo.reserve_pot_investment_stages(reserve_pot_id,stage_order,investment_type,provider,expected_annual_return,start_date,end_date,notes)
VALUES(@potId,@order,@type,@provider,@rate,@start,@end,@notes)",("@potId",potId),("@order",Math.Max(1,stageOrder)),("@type",investmentType.Trim()),("@provider",string.IsNullOrWhiteSpace(provider)?"Other":provider.Trim()),("@rate",expectedAnnualReturn),("@start",startDate.Date),("@end",endDate.HasValue?endDate.Value.Date:DBNull.Value),("@notes",DbValue(notes)));
        else
            await ExecuteAsync(@"UPDATE dbo.reserve_pot_investment_stages SET stage_order=@order,investment_type=@type,provider=@provider,expected_annual_return=@rate,start_date=@start,end_date=@end,notes=@notes,updated_at=SYSUTCDATETIME() WHERE reserve_pot_investment_stage_id=@id AND reserve_pot_id=@potId",("@id",id),("@potId",potId),("@order",Math.Max(1,stageOrder)),("@type",investmentType.Trim()),("@provider",string.IsNullOrWhiteSpace(provider)?"Other":provider.Trim()),("@rate",expectedAnnualReturn),("@start",startDate.Date),("@end",endDate.HasValue?endDate.Value.Date:DBNull.Value),("@notes",DbValue(notes)));
        await AddFinanceEventAsync("Household Reserve", id <= 0 ? "InvestmentStageCreated" : "InvestmentStageUpdated", "ReservePot", potId, "Investment journey updated", $"Stage {Math.Max(1,stageOrder)}: {investmentType.Trim()} at {expectedAnnualReturn:0.##}% expected annual return.", null, "User");
    }

    public async Task DeleteReservePotInvestmentStageAsync(int id, int potId)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("DELETE FROM dbo.reserve_pot_investment_stages WHERE reserve_pot_investment_stage_id=@id AND reserve_pot_id=@potId",("@id",id),("@potId",potId));
        await AddFinanceEventAsync("Household Reserve", "InvestmentStageDeleted", "ReservePot", potId, "Investment journey stage deleted", null, null, "User");
    }

    public async Task DeleteReservePotAsync(int id)
    {
        await EnsureModernTablesAsync();
        var pot = (await GetReservePotsAsync()).FirstOrDefault(x => x.Id == id);
        if (pot is not null) await AddFinanceEventAsync("Household Reserve", "PotDeleted", "ReservePot", id, $"{pot.Name} deleted", "The virtual allocation was deleted.", pot.AllocatedAmount, "User");
        await ExecuteAsync("DELETE FROM dbo.reserve_pots WHERE reserve_pot_id=@id", ("@id",id));
    }

    public async Task<List<MonthlyEntryTemplate>> GetMonthlyEntryTemplatesAsync(string source, bool activeOnly = true)
    {
        await EnsureModernTablesAsync();
        var templates = new List<MonthlyEntryTemplate>();
        const string sql = @"SELECT monthly_entry_template_id,source,[name],default_amount,category,[type],[length],notes,is_active
FROM dbo.monthly_entry_templates
WHERE source=@source AND (@activeOnly=0 OR is_active=1)
ORDER BY [name]";

        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@activeOnly", activeOnly);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            templates.Add(new MonthlyEntryTemplate(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8)));
        }
        return templates;
    }

    public async Task UpsertMonthlyEntryTemplateAsync(
        string source,
        string name,
        decimal defaultAmount,
        string? category,
        string? type,
        string? length,
        string? notes)
    {
        await EnsureModernTablesAsync();
        if (source == "extra_expenses")
            throw new InvalidOperationException("Extra expenses are one-off entries and cannot be permanent.");

        const string sql = @"MERGE dbo.monthly_entry_templates AS target
USING (SELECT @source AS source, @name AS [name]) AS incoming
ON target.source=incoming.source AND LOWER(LTRIM(RTRIM(target.[name])))=LOWER(LTRIM(RTRIM(incoming.[name])))
WHEN MATCHED THEN UPDATE SET [name]=@name,default_amount=@amount,category=@category,[type]=@type,[length]=@length,notes=@notes,is_active=1,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(source,[name],default_amount,category,[type],[length],notes,is_active)
VALUES(@source,@name,@amount,@category,@type,@length,@notes,1);";
        await ExecuteAsync(sql,
            ("@source", source), ("@name", name.Trim()), ("@amount", defaultAmount),
            ("@category", DbValue(category)), ("@type", DbValue(type)),
            ("@length", DbValue(length)), ("@notes", DbValue(notes)));
    }

    public async Task SetMonthlyEntryTemplateActiveAsync(string source, string name, bool isActive)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"UPDATE dbo.monthly_entry_templates
SET is_active=@isActive,updated_at=SYSUTCDATETIME()
WHERE source=@source AND LOWER(LTRIM(RTRIM([name])))=LOWER(LTRIM(RTRIM(@name)))",
            ("@source", source), ("@name", name.Trim()), ("@isActive", isActive));
    }


    private async Task<Dictionary<string, ExistingPaymentOption>> GetLatestPaymentOptionsBeforeMonthAsync(
        string source,
        int year,
        int month)
    {
        await EnsureModernTablesAsync();
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Date: "[date]", Category: "NULL", Type: "[type]", Length: "[length]", Notes: "[description]"),
            "everyday_spending" => (Table: "dbo.everyday_spending", Date: "[date]", Category: "category", Type: "[type]", Length: "[length]", Notes: "[description]"),
            "investments" => (Table: "dbo.investments", Date: "[date]", Category: "category", Type: "NULL", Length: "[length]", Notes: "notes"),
            "savings" => (Table: "dbo.savings", Date: "[date]", Category: "NULL", Type: "NULL", Length: "[length]", Notes: "notes"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        var sql = $@"WITH ranked AS
(
    SELECT [name], amount, {map.Category} AS category, {map.Type} AS [type],
           {map.Length} AS [length], {map.Notes} AS notes,
           ROW_NUMBER() OVER(PARTITION BY LOWER(LTRIM(RTRIM([name]))) ORDER BY {map.Date} DESC) AS rn
    FROM {map.Table}
    WHERE {map.Date} < @monthStart
)
SELECT [name], amount, category, [type], [length], notes
FROM ranked
WHERE rn = 1;";

        var items = new Dictionary<string, ExistingPaymentOption>(StringComparer.OrdinalIgnoreCase);
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@monthStart", new DateTime(year, month, 1));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new ExistingPaymentOption(
                reader.GetString(0),
                reader.GetDecimal(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5));
            items[item.Name.Trim()] = item;
        }

        return items;
    }

    public async Task<List<MonthlyEntryTemplate>> GetMissingMonthlyEntryTemplatesAsync(string source, int year, int month)
    {
        var templates = await GetMonthlyEntryTemplatesAsync(source);
        var rows = await GetRowsAsync(source, month, year);
        var existingNames = rows.Select(x => x.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedMonth = new DateTime(year, month, 1);
        var previousMonth = selectedMonth.AddMonths(-1);
        var previousMonthNames = (await GetRowsAsync(source, previousMonth.Month, previousMonth.Year))
            .Select(x => x.Name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var latestValues = await GetLatestPaymentOptionsBeforeMonthAsync(source, year, month);

        var recurringEntries = templates
            // A recurring definition is only offered when the entry still existed in
            // the immediately preceding month. This prevents old or deleted templates
            // from resurfacing in an unrelated workspace months later.
            .Where(x => previousMonthNames.Contains(x.Name.Trim()))
            .Where(x => !existingNames.Contains(x.Name.Trim()))
            .Select(template =>
            {
                if (!latestValues.TryGetValue(template.Name.Trim(), out var latest))
                    return template;

                return template with
                {
                    DefaultAmount = latest.Amount,
                    Category = latest.Category,
                    Type = latest.Type,
                    Length = latest.Length,
                    Notes = latest.Notes
                };
            });

        if (source == "savings")
        {
            var availablePots = (await GetReservePotsAsync())
                .Where(x => x.IsActive && !x.IsPausedFor(new DateTime(year, month, 1)))
                .Select(x => x.Name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            recurringEntries = recurringEntries.Where(x => availablePots.Contains(x.Name));
        }

        return recurringEntries.ToList();
    }

    public async Task<int> SetupMonthFromTemplatesAsync(int year, int month, string source, IEnumerable<MonthSetupItemInput> items)
    {
        await EnsureModernTablesAsync();
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (source == "extra_expenses") return 0;

        var selected = items.Where(x => x.Include && x.Amount >= 0).ToList();
        if (selected.Count == 0) return 0;

        var eligibleTemplates = await GetMissingMonthlyEntryTemplatesAsync(source, year, month);
        var selectedIds = selected.Select(x => x.TemplateId).ToHashSet();
        var allowed = eligibleTemplates
            .Where(x => selectedIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var existing = (await GetRowsAsync(source, month, year))
            .Select(x => x.Name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var date = new DateTime(year, month, Math.Min(DateTime.Today.Day, DateTime.DaysInMonth(year, month)));
        var added = 0;

        HashSet<string>? availablePots = null;
        if (source == "savings")
        {
            availablePots = (await GetReservePotsAsync())
                .Where(x => x.IsActive && !x.IsPausedFor(new DateTime(year, month, 1)))
                .Select(x => x.Name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var input in selected)
        {
            if (!allowed.TryGetValue(input.TemplateId, out var template)) continue;
            if (existing.Contains(template.Name)) continue;
            if (availablePots is not null && !availablePots.Contains(template.Name)) continue;

            await AddPaymentAsync(source, template.Name, input.Amount, date, template.Category, template.Type, template.Length, template.Notes);
            if (source == "savings" && input.Amount > 0)
                await ApplyReserveAllocationAsync(template.Name, input.Amount);
            existing.Add(template.Name);
            added++;
        }

        return added;
    }

    public async Task<StatementReconciliationIndexViewModel> GetStatementReconciliationIndexAsync(int year, int month)
    {
        await EnsureModernTablesAsync();
        var rows = new List<StatementAccountSummary>();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        const string sql = @"
SELECT a.account_balance_id,a.[name],a.provider,a.last_four_digits,a.is_active,
       s.bank_statement_id,s.[status],s.uploaded_at,
       COUNT(t.bank_statement_transaction_id) transaction_count,
       SUM(CASE WHEN t.[status]='Matched' THEN 1 ELSE 0 END) matched_count,
       SUM(CASE WHEN t.[status]='Unmatched' THEN 1 ELSE 0 END) unmatched_count,
       SUM(CASE WHEN t.[status]='Ignored' THEN 1 ELSE 0 END) ignored_count
FROM dbo.account_balances a
LEFT JOIN dbo.bank_statements s ON s.account_balance_id=a.account_balance_id AND s.[year]=@year AND s.[month]=@month
LEFT JOIN dbo.bank_statement_transactions t ON t.bank_statement_id=s.bank_statement_id
WHERE a.usage_type IN ('StatementOnly','Both')
GROUP BY a.account_balance_id,a.[name],a.provider,a.last_four_digits,a.is_active,s.bank_statement_id,s.[status],s.uploaded_at
ORDER BY a.is_active DESC,a.[name];";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new StatementAccountSummary(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? "No account reference" : $"Ending {reader.GetString(3)}", reader.GetBoolean(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5), reader.IsDBNull(6) ? "Awaiting statement" : reader.GetString(6),
                reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7)));
        }
        return new StatementReconciliationIndexViewModel { Year = year, Month = month, Accounts = rows };
    }

    public async Task<long> GetOrCreateStatementAsync(int accountId, int year, int month)
    {
        await EnsureModernTablesAsync();
        var existing = await ScalarAsync("SELECT bank_statement_id FROM dbo.bank_statements WHERE account_balance_id=@accountId AND [year]=@year AND [month]=@month", ("@accountId", accountId), ("@year", year), ("@month", month));
        if (existing is not null && existing != DBNull.Value) return Convert.ToInt64(existing);
        await ExecuteAsync("INSERT INTO dbo.bank_statements(account_balance_id,[year],[month]) VALUES(@accountId,@year,@month)", ("@accountId", accountId), ("@year", year), ("@month", month));
        return Convert.ToInt64(await ScalarAsync("SELECT bank_statement_id FROM dbo.bank_statements WHERE account_balance_id=@accountId AND [year]=@year AND [month]=@month", ("@accountId", accountId), ("@year", year), ("@month", month)));
    }

    public async Task AttachStatementFileAsync(long statementId, string originalName, string storedName)
        => await ExecuteAsync("UPDATE dbo.bank_statements SET original_file_name=@original,stored_file_name=@stored,uploaded_at=SYSUTCDATETIME(),[status]=CASE WHEN [status]='Completed' THEN [status] ELSE 'In review' END WHERE bank_statement_id=@id", ("@original", originalName), ("@stored", storedName), ("@id", statementId));

    public async Task AddStatementTransactionAsync(long statementId, DateTime date, string description, decimal amount, string direction)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync("INSERT INTO dbo.bank_statement_transactions(bank_statement_id,transaction_date,[description],amount,direction) VALUES(@statementId,@date,@description,@amount,@direction); UPDATE dbo.bank_statements SET [status]='In review' WHERE bank_statement_id=@statementId AND [status]<>'Completed'", ("@statementId", statementId), ("@date", date.Date), ("@description", description), ("@amount", amount), ("@direction", direction));
    }

    public async Task<StatementWorkspaceViewModel?> GetStatementWorkspaceAsync(long statementId)
    {
        await EnsureModernTablesAsync();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        const string headerSql = @"SELECT s.bank_statement_id,a.account_balance_id,a.[name],a.provider,s.[year],s.[month],s.[status],s.original_file_name,s.uploaded_at FROM dbo.bank_statements s INNER JOIN dbo.account_balances a ON a.account_balance_id=s.account_balance_id WHERE s.bank_statement_id=@id";
        await using var header = new SqlCommand(headerSql, con);
        header.Parameters.AddWithValue("@id", statementId);
        int accountId, year, month; string accountName, provider, status; string? file; DateTime? uploaded;
        await using (var reader = await header.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return null;
            accountId=reader.GetInt32(1); accountName=reader.GetString(2); provider=reader.GetString(3); year=reader.GetInt32(4); month=reader.GetInt32(5); status=reader.GetString(6); file=reader.IsDBNull(7)?null:reader.GetString(7); uploaded=reader.IsDBNull(8)?null:reader.GetDateTime(8);
        }
        var transactions = new List<StatementTransactionRow>();
        await using (var cmd = new SqlCommand("SELECT bank_statement_transaction_id,transaction_date,[description],amount,direction,[status],matched_source,matched_entry_id,match_label,notes FROM dbo.bank_statement_transactions WHERE bank_statement_id=@id ORDER BY transaction_date,bank_statement_transaction_id", con))
        {
            cmd.Parameters.AddWithValue("@id", statementId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while(await reader.ReadAsync()) transactions.Add(new StatementTransactionRow(reader.GetInt64(0),reader.GetDateTime(1),reader.GetString(2),reader.GetDecimal(3),reader.GetString(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),reader.IsDBNull(7)?null:reader.GetInt32(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.IsDBNull(9)?null:reader.GetString(9)));
        }
        var candidates = await GetFinanceEntryCandidatesAsync(accountId, year, month, con);
        return new StatementWorkspaceViewModel { StatementId=statementId,AccountId=accountId,AccountName=accountName,Provider=provider,Year=year,Month=month,Status=status,OriginalFileName=file,UploadedAt=uploaded,Transactions=transactions,Candidates=candidates };
    }

    private static async Task<List<FinanceEntryCandidate>> GetFinanceEntryCandidatesAsync(int accountId, int year, int month, SqlConnection con)
    {
        var result = new List<FinanceEntryCandidate>();
        var start = new DateTime(year,month,1); var end=start.AddMonths(1);
        var sources = new[] { ("bills","billid","date","Out"), ("everyday_spending","everyday_spending_id","date","Out"), ("extra_expenses","extra_expense_id","duedate","Out"), ("investments","investments_id","date","Out"), ("savings","savings_id","date","Out") };
        foreach (var (table,idCol,dateCol,direction) in sources)
        {
            var sql=$"SELECT {idCol},{dateCol},[name],amount FROM dbo.{table} WHERE account_balance_id=@accountId AND {dateCol}>=@start AND {dateCol}<@end ORDER BY {dateCol}";
            await using var cmd=new SqlCommand(sql,con); cmd.Parameters.AddWithValue("@accountId",accountId);cmd.Parameters.AddWithValue("@start",start);cmd.Parameters.AddWithValue("@end",end);
            await using var reader=await cmd.ExecuteReaderAsync();
            while(await reader.ReadAsync()) result.Add(new FinanceEntryCandidate($"{table}:{reader.GetInt32(0)}",table,reader.GetInt32(0),reader.GetDateTime(1),reader.GetString(2),reader.GetDecimal(3),direction));
        }
        return result.OrderBy(x=>x.Date).ThenBy(x=>x.Name).ToList();
    }

    public async Task MatchStatementTransactionAsync(long transactionId, string candidateKey)
    {
        var parts=(candidateKey??"").Split(':',2); if(parts.Length!=2 || !int.TryParse(parts[1],out var id)) throw new InvalidOperationException("Select a valid Finance Manager entry.");
        var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"bills","everyday_spending","extra_expenses","investments","savings"}; if(!allowed.Contains(parts[0])) throw new InvalidOperationException("Unsupported entry source.");
        var map=parts[0] switch { "bills"=>("billid","date"), "everyday_spending"=>("everyday_spending_id","date"), "extra_expenses"=>("extra_expense_id","duedate"), "investments"=>("investments_id","date"), _=>("savings_id","date") };
        await using var con=new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd=new SqlCommand($"SELECT [name],amount,{map.Item2} FROM dbo.{parts[0]} WHERE {map.Item1}=@id",con);cmd.Parameters.AddWithValue("@id",id);
        string label; await using(var reader=await cmd.ExecuteReaderAsync()){ if(!await reader.ReadAsync()) throw new InvalidOperationException("The selected entry no longer exists."); label=$"{reader.GetDateTime(2):dd MMM} · {reader.GetString(0)} · {reader.GetDecimal(1):C}"; }
        await ExecuteAsync("UPDATE dbo.bank_statement_transactions SET [status]='Matched',matched_source=@source,matched_entry_id=@entryId,match_label=@label,notes=NULL,updated_at=SYSUTCDATETIME() WHERE bank_statement_transaction_id=@id",("@source",parts[0]),("@entryId",id),("@label",label),("@id",transactionId));
    }

    public async Task SetStatementTransactionStatusAsync(long transactionId,string status,string? notes)
        => await ExecuteAsync("UPDATE dbo.bank_statement_transactions SET [status]=@status,matched_source=NULL,matched_entry_id=NULL,match_label=NULL,notes=@notes,updated_at=SYSUTCDATETIME() WHERE bank_statement_transaction_id=@id",("@status",status),("@notes",(object?)notes??DBNull.Value),("@id",transactionId));

    public async Task<bool> CompleteStatementAsync(long statementId)
    {
        var remaining=Convert.ToInt32(await ScalarAsync("SELECT COUNT(*) FROM dbo.bank_statement_transactions WHERE bank_statement_id=@id AND [status]='Unmatched'",("@id",statementId)) ?? 0);
        if(remaining>0) return false;
        await ExecuteAsync("UPDATE dbo.bank_statements SET [status]='Completed',completed_at=SYSUTCDATETIME() WHERE bank_statement_id=@id",("@id",statementId)); return true;
    }

}
