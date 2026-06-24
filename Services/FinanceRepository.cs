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
IF OBJECT_ID('dbo.finance_settings','U') IS NULL
CREATE TABLE dbo.finance_settings([key] nvarchar(120) NOT NULL PRIMARY KEY, [value] nvarchar(300) NOT NULL, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.account_balances','U') IS NULL
CREATE TABLE dbo.account_balances(account_balance_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(120) NOT NULL, amount decimal(18,2) NOT NULL, interest_rate decimal(9,4) NOT NULL, monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, include_in_global_goal bit NOT NULL DEFAULT 1, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.account_balance_history','U') IS NULL
CREATE TABLE dbo.account_balance_history(history_id int IDENTITY(1,1) PRIMARY KEY, account_balance_id int NULL, [name] nvarchar(120) NOT NULL, amount decimal(18,2) NOT NULL, interest_rate decimal(9,4) NOT NULL, monthly_contribution decimal(18,2) NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.monthly_income_stats','U') IS NULL
CREATE TABLE dbo.monthly_income_stats(income_id int IDENTITY(1,1) PRIMARY KEY, [year] int NOT NULL, [month] int NOT NULL, amount decimal(18,2) NOT NULL, sick_days int NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT UQ_monthly_income_stats UNIQUE([year],[month]));
IF OBJECT_ID('dbo.saving_pots','U') IS NULL
CREATE TABLE dbo.saving_pots(saving_pot_id int IDENTITY(1,1) PRIMARY KEY, [name] nvarchar(120) NOT NULL, target_amount decimal(18,2) NOT NULL, monthly_amount decimal(18,2) NOT NULL, created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.saving_pot_months','U') IS NULL
CREATE TABLE dbo.saving_pot_months(saving_pot_month_id int IDENTITY(1,1) PRIMARY KEY, saving_pot_id int NOT NULL, [year] int NOT NULL, [month] int NOT NULL, is_saved bit NOT NULL DEFAULT 0, updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_saving_pot_months_pots FOREIGN KEY(saving_pot_id) REFERENCES dbo.saving_pots(saving_pot_id) ON DELETE CASCADE, CONSTRAINT UQ_saving_pot_months UNIQUE(saving_pot_id,[year],[month]));
IF NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE [name]='Lucy''s ISA') INSERT INTO dbo.account_balances([name], amount, interest_rate, monthly_contribution, include_in_global_goal) VALUES('Lucy''s ISA',4000,3.8,0,1);
IF NOT EXISTS (SELECT 1 FROM dbo.account_balances WHERE [name]='Monzo Pots') INSERT INTO dbo.account_balances([name], amount, interest_rate, monthly_contribution, include_in_global_goal) VALUES('Monzo Pots',1370,2.75,0,1);";
        await ExecuteAsync(sql);
    }

    public async Task<List<PaymentRow>> GetRowsAsync(string source, int month, int year)
    {
        var map = source switch
        {
            "bills" => (Table:"dbo.bills", Id:"billid", Date:"[date]", Category:"NULL", Type:"type", Length:"length", Notes:"description"),
            "extra_expenses" => (Table:"dbo.extra_expenses", Id:"extra_expense_id", Date:"duedate", Category:"category", Type:"type", Length:"length", Notes:"description"),
            "investments" => (Table:"dbo.investments", Id:"investments_id", Date:"[date]", Category:"category", Type:"NULL", Length:"length", Notes:"notes"),
            "savings" => (Table:"dbo.savings", Id:"savings_id", Date:"[date]", Category:"NULL", Type:"NULL", Length:"length", Notes:"notes"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        string sql = $@"SELECT {map.Id} AS id, [name], amount, {map.Date} AS [date], {map.Category} AS category, {map.Type} AS [type], {map.Length} AS [length], {map.Notes} AS notes
FROM {map.Table} WHERE MONTH({map.Date})=@month AND YEAR({map.Date})=@year ORDER BY {map.Date} DESC";
        var rows = new List<PaymentRow>();
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@month", month); cmd.Parameters.AddWithValue("@year", year);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) rows.Add(new PaymentRow(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDateTime(3), r.IsDBNull(4)?null:r.GetString(4), r.IsDBNull(5)?null:r.GetString(5), r.IsDBNull(6)?null:r.GetString(6), r.IsDBNull(7)?null:r.GetString(7), source));
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
        var accounts = new List<AccountBalance> { new(0, "Emergency Fund", emergencyFund, await GetDecimalSettingAsync("EmergencyFundInterestRate",3.8m), 0, true, await GetEmergencyFundUpdatedAsync() ?? DateTime.MinValue) };
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

    public async Task SaveIncomeAsync(int year,int month,decimal amount,int sickDays)
    {
        await EnsureModernTablesAsync();
        await ExecuteAsync(@"MERGE dbo.monthly_income_stats AS t USING (SELECT @year y,@month m) AS s ON t.[year]=s.y AND t.[month]=s.m WHEN MATCHED THEN UPDATE SET amount=@amount,sick_days=@sick,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([year],[month],amount,sick_days) VALUES(@year,@month,@amount,@sick);", ("@year",year),("@month",month),("@amount",amount),("@sick",sickDays));
        await ExecuteAsync(@"MERGE dbo.monthly_allowance AS t USING (SELECT @month m) AS s ON t.month_id=s.m WHEN MATCHED THEN UPDATE SET amount=@amount WHEN NOT MATCHED THEN INSERT(month_id, amount) VALUES(@month,@amount);", ("@month",month),("@amount",amount));
    }

    public async Task SaveAccountAsync(int id,string name,decimal amount,decimal rate,decimal monthly,bool include)
    {
        await EnsureModernTablesAsync();
        if (id == 0 && name == "Emergency Fund") { await ExecuteAsync("IF EXISTS (SELECT 1 FROM dbo.emergency_fund) UPDATE dbo.emergency_fund SET amount=@amount,updated_at=GETDATE() ELSE INSERT INTO dbo.emergency_fund(amount,updated_at) VALUES(@amount,GETDATE())", ("@amount",amount)); await ExecuteAsync("MERGE dbo.finance_settings AS t USING (SELECT @key AS [key]) AS s ON t.[key]=s.[key] WHEN MATCHED THEN UPDATE SET [value]=@value, updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([key],[value]) VALUES(@key,@value);", ("@key","EmergencyFundInterestRate"),("@value",rate)); return; }
        if (id == 0) await ExecuteAsync("INSERT INTO dbo.account_balances([name],amount,interest_rate,monthly_contribution,include_in_global_goal) VALUES(@name,@amount,@rate,@monthly,@include)", ("@name",name),("@amount",amount),("@rate",rate),("@monthly",monthly),("@include",include));
        else await ExecuteAsync("UPDATE dbo.account_balances SET [name]=@name,amount=@amount,interest_rate=@rate,monthly_contribution=@monthly,include_in_global_goal=@include,updated_at=SYSUTCDATETIME() WHERE account_balance_id=@id", ("@id",id),("@name",name),("@amount",amount),("@rate",rate),("@monthly",monthly),("@include",include));
        await ExecuteAsync("INSERT INTO dbo.account_balance_history(account_balance_id,[name],amount,interest_rate,monthly_contribution) VALUES(@id,@name,@amount,@rate,@monthly)", ("@id",id),("@name",name),("@amount",amount),("@rate",rate),("@monthly",monthly));
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
            "bills" => (Table:"dbo.bills", Id:"billid", Date:"[date]", Category:"NULL", Type:"type", Length:"length", Notes:"description", IdParam:"@id"),
            "extra_expenses" => (Table:"dbo.extra_expenses", Id:"extra_expense_id", Date:"duedate", Category:"category", Type:"type", Length:"length", Notes:"description", IdParam:"@id"),
            "investments" => (Table:"dbo.investments", Id:"investments_id", Date:"[date]", Category:"category", Type:"NULL", Length:"length", Notes:"notes", IdParam:"@id"),
            "savings" => (Table:"dbo.savings", Id:"savings_id", Date:"[date]", Category:"NULL", Type:"NULL", Length:"length", Notes:"notes", IdParam:"@id"),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        var sql = $"SELECT {map.Id} AS id, [name], amount, {map.Date} AS [date], {map.Category} AS category, {map.Type} AS [type], {map.Length} AS [length], {map.Notes} AS notes FROM {map.Table} WHERE {map.Id}=@id";
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync()) return new PaymentRow(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDateTime(3), r.IsDBNull(4)?null:r.GetString(4), r.IsDBNull(5)?null:r.GetString(5), r.IsDBNull(6)?null:r.GetString(6), r.IsDBNull(7)?null:r.GetString(7), source);
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

    public async Task<decimal> GetDecimalSettingAsync(string key, decimal fallback) { var db = await ScalarAsync("IF OBJECT_ID('dbo.finance_settings','U') IS NOT NULL SELECT [value] FROM dbo.finance_settings WHERE [key]=@key", ("@key", key)); return decimal.TryParse(Convert.ToString(db), out var v) ? v : (decimal.TryParse(config[$"FinanceSettings:{key}"], out var c) ? c : fallback); }
    public async Task SaveDecimalSettingAsync(string key, decimal value) { await EnsureModernTablesAsync(); await ExecuteAsync("MERGE dbo.finance_settings AS t USING (SELECT @key AS [key]) AS s ON t.[key]=s.[key] WHEN MATCHED THEN UPDATE SET [value]=@value, updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT([key],[value]) VALUES(@key,@value);", ("@key", key), ("@value", value)); }
    private async Task<object?> ScalarAsync(string sql, params (string, object)[] ps) { await using var con = new SqlConnection(ConnStr); await con.OpenAsync(); await using var cmd = new SqlCommand(sql, con); foreach(var p in ps) cmd.Parameters.AddWithValue(p.Item1,p.Item2); return await cmd.ExecuteScalarAsync(); }
    private async Task ExecuteAsync(string sql, params (string, object)[] ps) { await using var con = new SqlConnection(ConnStr); await con.OpenAsync(); await using var cmd = new SqlCommand(sql, con); foreach(var p in ps) cmd.Parameters.AddWithValue(p.Item1,p.Item2); await cmd.ExecuteNonQueryAsync(); }

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
        await using var con = new SqlConnection(ConnStr); await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT saving_pot_month_id,saving_pot_id,[year],[month],is_saved,updated_at FROM dbo.saving_pot_months WHERE [year]=@year ORDER BY saving_pot_id,[month]", con);
        cmd.Parameters.AddWithValue("@year", year);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SavingPotMonth(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetBoolean(4), r.GetDateTime(5)));
        }
        return list;
    }

    public async Task<decimal> GetTotalAllocatedToSavingPotsAsync()
    {
        await EnsureModernTablesAsync();
        var value = await ScalarAsync(@"SELECT COALESCE(SUM(p.monthly_amount),0)
            FROM dbo.saving_pot_months m
            INNER JOIN dbo.saving_pots p ON p.saving_pot_id=m.saving_pot_id
            WHERE m.is_saved=1");
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
        await ExecuteAsync(@"MERGE dbo.saving_pot_months AS t
USING (SELECT @potId AS pot_id, @year AS y, @month AS m) AS s
ON t.saving_pot_id=s.pot_id AND t.[year]=s.y AND t.[month]=s.m
WHEN MATCHED THEN UPDATE SET is_saved = CASE WHEN is_saved=1 THEN 0 ELSE 1 END, updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(saving_pot_id,[year],[month],is_saved) VALUES(@potId,@year,@month,1);", ("@potId", potId), ("@year", year), ("@month", month));
    }

}
