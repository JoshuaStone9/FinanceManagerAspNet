using FinanceManagerAspNet.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FinanceManagerAspNet.Services;

public sealed class FinanceRepository(IConfiguration config)
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

IF OBJECT_ID('dbo.account_balance_history','U') IS NULL
CREATE TABLE dbo.account_balance_history(history_id int IDENTITY(1,1) PRIMARY KEY, account_balance_id int NULL, [name] nvarchar(120) NOT NULL, amount decimal(18,2) NOT NULL, interest_rate decimal(9,4) NOT NULL, monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID('dbo.monthly_income_stats','U') IS NULL
CREATE TABLE dbo.monthly_income_stats(income_id int IDENTITY(1,1) PRIMARY KEY, [year] int NOT NULL, [month] int NOT NULL, amount decimal(18,2) NOT NULL, sick_days int NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT UQ_monthly_income_stats UNIQUE([year],[month]));

IF OBJECT_ID('dbo.saving_pots','U') IS NULL
CREATE TABLE dbo.saving_pots(saving_pot_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(120) NOT NULL, target_amount decimal(18,2) NOT NULL, monthly_amount decimal(18,2) NOT NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());

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
        var accounts = new List<AccountBalance> { new(0, "Emergency Fund", emergencyFund, await GetDecimalSettingAsync("EmergencyFundInterestRate", 3.8m), 0, true, await GetEmergencyFundUpdatedAsync() ?? DateTime.MinValue) };
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT account_balance_id,[name],amount,interest_rate,monthly_contribution,include_in_global_goal,updated_at FROM dbo.account_balances ORDER BY [name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) accounts.Add(new AccountBalance(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetBoolean(5), r.GetDateTime(6)));
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

    public async Task<PaymentRow?> GetPaymentAsync(string source, int id)
    {
        var map = source switch
        {
            "bills" => (Table: "dbo.bills", Id: "billid", Date: "[date]", Category: "NULL", Type: "type", Length: "length", Notes: "description", IdParam: "@id"),
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
        await using var cmd = new SqlCommand("SELECT saving_pot_id,[name],target_amount,monthly_amount,created_at,updated_at FROM dbo.saving_pots ORDER BY [name]", con);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SavingPot(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDateTime(4), r.GetDateTime(5)));
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

    public async Task SaveSavingPotAsync(int id, string name, decimal targetAmount, decimal monthlyAmount)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pot name is required.", nameof(name));
        await EnsureModernTablesAsync();
        if (id == 0)
        {
            await ExecuteAsync("INSERT INTO dbo.saving_pots([name],target_amount,monthly_amount) VALUES(@name,@target,@monthly)", ("@name", name.Trim()), ("@target", targetAmount), ("@monthly", monthlyAmount));
        }
        else
        {
            await ExecuteAsync("UPDATE dbo.saving_pots SET [name]=@name,target_amount=@target,monthly_amount=@monthly,updated_at=SYSUTCDATETIME() WHERE saving_pot_id=@id", ("@id", id), ("@name", name.Trim()), ("@target", targetAmount), ("@monthly", monthlyAmount));
        }
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
}
