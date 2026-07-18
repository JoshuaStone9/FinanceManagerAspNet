using System.Text;
using System.Text.Json;
using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FinanceManagerAspNet.Controllers;

public sealed class SettingsController(
    FinanceRepository repo,
    AppAuthService authService,
    IConfiguration configuration) : Controller
{
    private static readonly string[] BackupTables =
    [
        "finance_settings", "monthly_allowance", "monthly_income_stats", "monthly_income_entries", "passive_income_records", "account_reconciliations",
        "bills", "everyday_spending", "extra_expenses", "investments", "savings",
        "monthly_entry_templates", "monthly_carry_forward", "emergency_fund", "household_reserve",
        "reserve_account_selections", "reserve_pots", "reserve_pot_monthly_funding", "reserve_pot_actions",
        "reserve_pot_recovery_allocations", "finance_events", "finance_reminders",
        "account_balances", "account_balance_history", "asset_holdings", "reserved_funds",
        "forecast_scenarios", "recommendation_applications", "saving_pots", "saving_pot_months",
        "saving_pot_extras", "savings_contribution_changes"
    ];

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new ApplicationSettingsViewModel
        {
            Finance = new FinanceSettingsInput
            {
                EmergencyFundBaseline = await repo.GetDecimalSettingAsync("EmergencyFundBaseline", 12000m),
                ForecastMethod = await repo.GetStringSettingAsync("ForecastMethod", "Last3Months"),
                CurrencyCode = await repo.GetStringSettingAsync("CurrencyCode", "GBP"),
                ShowPence = await repo.GetBoolSettingAsync("ShowPence", true),
                PrepareRecurringEntriesByDefault = await repo.GetBoolSettingAsync("PrepareRecurringEntriesByDefault", true),
                PauseCompletedPotsAutomatically = await repo.GetBoolSettingAsync("PauseCompletedPotsAutomatically", false)
            },
            Appearance = new AppearanceSettingsInput
            {
                Theme = await repo.GetStringSettingAsync("Theme", "Dark"),
                Accent = await repo.GetStringSettingAsync("Accent", "Purple"),
                CompactMode = await repo.GetBoolSettingAsync("CompactMode", false),
                SidebarCollapsedByDefault = await repo.GetBoolSettingAsync("SidebarCollapsedByDefault", false)
            },
            Security = new SecuritySettingsInput
            {
                RememberDays = await repo.GetIntSettingAsync("RememberDays", 365),
                SessionTimeoutMinutes = await repo.GetIntSettingAsync("SessionTimeoutMinutes", 60),
                ReadOnlyWhenLoggedOut = await repo.GetBoolSettingAsync("ReadOnlyWhenLoggedOut", true)
            },
            Diagnostics = await BuildDiagnosticsAsync(),
            AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.0",
            DatabaseName = new SqlConnectionStringBuilder(GetConnectionString()).InitialCatalog,
            CanEdit = User.Identity?.IsAuthenticated == true
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFinance([Bind(Prefix = "Finance")] FinanceSettingsInput input)
    {
        if (!CanEdit()) return Unauthorized();
        if (!ModelState.IsValid) return await ReturnInvalidAsync("finance");

        await repo.SaveDecimalSettingAsync("EmergencyFundBaseline", input.EmergencyFundBaseline);
        await repo.SaveStringSettingAsync("ForecastMethod", Normalise(input.ForecastMethod, "Last3Months"));
        await repo.SaveStringSettingAsync("CurrencyCode", Normalise(input.CurrencyCode, "GBP"));
        await repo.SaveBoolSettingAsync("ShowPence", input.ShowPence);
        await repo.SaveBoolSettingAsync("PrepareRecurringEntriesByDefault", input.PrepareRecurringEntriesByDefault);
        await repo.SaveBoolSettingAsync("PauseCompletedPotsAutomatically", input.PauseCompletedPotsAutomatically);
        TempData["Success"] = "Finance settings saved.";
        return RedirectToSettings("finance");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAppearance([Bind(Prefix = "Appearance")] AppearanceSettingsInput input)
    {
        if (!CanEdit()) return Unauthorized();
        await repo.SaveStringSettingAsync("Theme", Normalise(input.Theme, "Dark"));
        await repo.SaveStringSettingAsync("Accent", Normalise(input.Accent, "Purple"));
        await repo.SaveBoolSettingAsync("CompactMode", input.CompactMode);
        await repo.SaveBoolSettingAsync("SidebarCollapsedByDefault", input.SidebarCollapsedByDefault);
        TempData["Success"] = "Appearance settings saved. Refreshing applies the defaults to this browser.";
        return RedirectToSettings("appearance");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSecurity([Bind(Prefix = "Security")] SecuritySettingsInput input)
    {
        if (!CanEdit()) return Unauthorized();
        if (!ModelState.IsValid) return await ReturnInvalidAsync("security");
        await repo.SaveIntSettingAsync("RememberDays", input.RememberDays);
        await repo.SaveIntSettingAsync("SessionTimeoutMinutes", input.SessionTimeoutMinutes);
        await repo.SaveBoolSettingAsync("ReadOnlyWhenLoggedOut", input.ReadOnlyWhenLoggedOut);
        TempData["Success"] = "Security preferences saved. Remember-login changes apply at the next sign-in.";
        return RedirectToSettings("security");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (!CanEdit()) return Unauthorized();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Passwords must match and contain at least eight characters.";
            return RedirectToSettings("security");
        }
        await authService.SetPasswordAsync(input.NewPassword);
        TempData["Success"] = "Login password updated.";
        return RedirectToSettings("security");
    }

    [HttpGet]
    public async Task<IActionResult> ExportBackup()
    {
        if (!CanEdit()) return Unauthorized();
        var snapshot = await CreateSnapshotAsync();
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", $"finance-manager-backup-{DateTime.Now:yyyyMMdd-HHmm}.json");
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv()
    {
        if (!CanEdit()) return Unauthorized();
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        var csv = new StringBuilder("Section,Name,Amount,Date,Category,Notes\r\n");
        foreach (var (table, name, amount, date, category, notes) in new[]
        {
            ("monthly_income_entries", "name", "amount", "date", "category", "notes"),
            ("bills", "name", "amount", "date", "type", "description"),
            ("everyday_spending", "name", "amount", "date", "category", "description"),
            ("extra_expenses", "name", "amount", "duedate", "category", "description"),
            ("investments", "name", "amount", "date", "category", "notes")
        })
        {
            if (!await TableExistsAsync(connection, table)) continue;
            await using var command = new SqlCommand($"SELECT [{name}],[{amount}],[{date}],[{category}],[{notes}] FROM dbo.[{table}] ORDER BY [{date}]", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                csv.AppendLine(string.Join(',', Escape(table), Escape(reader[0]), Escape(reader[1]), Escape(reader[2]), Escape(reader[3]), Escape(reader[4])));
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"finance-manager-export-{DateTime.Now:yyyyMMdd}.csv");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreBackup(IFormFile backupFile)
    {
        if (!CanEdit()) return Unauthorized();
        if (backupFile is null || backupFile.Length == 0)
        {
            TempData["Error"] = "Choose a Finance Manager JSON backup first.";
            return RedirectToSettings("data");
        }

        try
        {
            await using var stream = backupFile.OpenReadStream();
            var snapshot = await JsonSerializer.DeserializeAsync<DatabaseSnapshot>(stream);
            if (snapshot is null || snapshot.Format != "FinanceManagerAspNet.Backup.v1") throw new InvalidDataException("Unsupported backup format.");
            await RestoreSnapshotAsync(snapshot);
            TempData["Success"] = "Backup restored successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Restore failed: {ex.Message}";
        }
        return RedirectToSettings("data");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ResetCurrentMonth()
    {
        var today = DateTime.Today;
        return ResetMonthAsync(today.Year, today.Month);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> ResetSelectedMonth(int year, int month)
        => ResetMonthAsync(year, month);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FactoryReset(string confirmation)
    {
        if (!CanEdit()) return Unauthorized();
        if (!string.Equals(confirmation?.Trim(), "DELETE EVERYTHING", StringComparison.Ordinal))
        {
            TempData["Error"] = "Type DELETE EVERYTHING exactly to confirm the factory reset.";
            return RedirectToSettings("data");
        }

        var tables = BackupTables.Where(x => x != "finance_settings").Reverse().ToArray();
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            foreach (var table in tables)
            {
                if (!await TableExistsAsync(connection, table, transaction)) continue;
                await using var command = new SqlCommand($"DELETE FROM dbo.[{table}]", connection, transaction);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            TempData["Success"] = "Factory reset complete. Application settings, security preferences and Personal Vault data were retained.";
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToSettings("data");
    }

    private async Task<IActionResult> ResetMonthAsync(int year, int month)
    {
        if (!CanEdit()) return Unauthorized();
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
        {
            TempData["Error"] = "Choose a valid month to reset.";
            return RedirectToSettings("data");
        }

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var deletedRows = 0;

        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            // Delete dependent monthly records before their parent monthly funding rows.
            deletedRows += await DeleteWhereAsync(connection, transaction, "passive_income_records", "[year]=@year AND [month]=@month", year, month, monthStart, monthEnd);

            deletedRows += await DeleteWhereAsync(connection, transaction, "reserve_pot_recovery_allocations",
                "([source_year]=@year AND [source_month]=@month) OR ([target_year]=@year AND [target_month]=@month)",
                year, month, monthStart, monthEnd);

            foreach (var table in new[] { "reserve_pot_monthly_funding", "saving_pot_months", "monthly_income_stats", "monthly_carry_forward" })
                deletedRows += await DeleteWhereAsync(connection, transaction, table, "[year]=@year AND [month]=@month", year, month, monthStart, monthEnd);

            foreach (var (table, column) in new[]
            {
                ("monthly_income_entries", "date"),
                ("bills", "date"),
                ("everyday_spending", "date"),
                ("extra_expenses", "duedate"),
                ("investments", "date"),
                ("savings", "date"),
                ("reserve_pot_actions", "action_date"),
                ("saving_pot_extras", "date")
            })
            {
                deletedRows += await DeleteWhereAsync(connection, transaction, table,
                    $"[{column}]>=@monthStart AND [{column}]<@monthEnd", year, month, monthStart, monthEnd);
            }

            // Legacy allowance records only store a month number, not a year.
            deletedRows += await DeleteWhereAsync(connection, transaction, "monthly_allowance", "[month_id]=@month", year, month, monthStart, monthEnd);

            await transaction.CommitAsync();
            var monthLabel = monthStart.ToString("MMMM yyyy");
            TempData["Success"] = $"{monthLabel} reset complete. {deletedRows} monthly record{(deletedRows == 1 ? string.Empty : "s")} removed; other months and permanent records were retained.";
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToSettings("data");
    }

    private static async Task<int> DeleteWhereAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        string predicate,
        int year,
        int month,
        DateTime monthStart,
        DateTime monthEnd)
    {
        if (!await TableExistsAsync(connection, table, transaction)) return 0;

        await using var command = new SqlCommand($"DELETE FROM dbo.[{table}] WHERE {predicate}", connection, transaction);
        command.Parameters.AddWithValue("@year", year);
        command.Parameters.AddWithValue("@month", month);
        command.Parameters.AddWithValue("@monthStart", monthStart);
        command.Parameters.AddWithValue("@monthEnd", monthEnd);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<IActionResult> ReturnInvalidAsync(string fragment)
    {
        TempData["Error"] = "Review the highlighted settings and try again.";
        return RedirectToSettings(fragment);
    }

    private IActionResult RedirectToSettings(string fragment)
    {
        var url = Url.Action(nameof(Index)) ?? "/Settings";
        return Redirect($"{url}#{fragment}");
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private static string Normalise(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private string GetConnectionString() => Environment.GetEnvironmentVariable("FM_CONNECTION_STRING") ?? configuration.GetConnectionString("FinanceManager") ?? throw new InvalidOperationException("Missing FinanceManager connection string.");

    private async Task<SettingsDiagnostics> BuildDiagnosticsAsync()
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();
            await using var tableCommand = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')", connection);
            await using var settingsCommand = new SqlCommand("SELECT COUNT(*) FROM dbo.finance_settings", connection);
            return new SettingsDiagnostics { DatabaseConnected = true, FinanceTableCount = Convert.ToInt32(await tableCommand.ExecuteScalarAsync()), SettingsCount = Convert.ToInt32(await settingsCommand.ExecuteScalarAsync()), CheckedAt = DateTime.UtcNow };
        }
        catch { return new SettingsDiagnostics { DatabaseConnected = false, CheckedAt = DateTime.UtcNow }; }
    }

    private async Task<DatabaseSnapshot> CreateSnapshotAsync()
    {
        var snapshot = new DatabaseSnapshot { CreatedAtUtc = DateTime.UtcNow };
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        foreach (var table in BackupTables)
        {
            if (!await TableExistsAsync(connection, table)) continue;
            await using var command = new SqlCommand($"SELECT * FROM dbo.[{table}]", connection);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, JsonElement>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object));
                }
                rows.Add(row);
            }
            snapshot.Tables[table] = rows;
        }
        return snapshot;
    }

    private async Task RestoreSnapshotAsync(DatabaseSnapshot snapshot)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        var restoreTables = new List<string>();
        foreach (var table in BackupTables)
        {
            if (snapshot.Tables.ContainsKey(table) && await TableExistsAsync(connection, table, transaction))
                restoreTables.Add(table);
        }

        try
        {
            // A backup preserves identity values and therefore also preserves foreign-key values.
            // Temporarily suspend checks so parent/child rows can be replaced safely regardless of
            // table ordering, then fully validate every relationship before committing.
            foreach (var table in restoreTables)
                await SetConstraintsEnabledAsync(connection, transaction, table, enabled: false);

            foreach (var table in restoreTables.AsEnumerable().Reverse())
                await new SqlCommand($"DELETE FROM dbo.[{table}]", connection, transaction).ExecuteNonQueryAsync();

            foreach (var table in restoreTables)
            {
                if (!snapshot.Tables.TryGetValue(table, out var rows) || rows.Count == 0) continue;

                var identity = await HasIdentityAsync(connection, table, transaction);
                try
                {
                    if (identity)
                        await new SqlCommand($"SET IDENTITY_INSERT dbo.[{table}] ON", connection, transaction).ExecuteNonQueryAsync();

                    foreach (var row in rows)
                        await InsertRowAsync(connection, transaction, table, row);
                }
                finally
                {
                    if (identity)
                        await new SqlCommand($"SET IDENTITY_INSERT dbo.[{table}] OFF", connection, transaction).ExecuteNonQueryAsync();
                }
            }

            foreach (var table in restoreTables)
                await SetConstraintsEnabledAsync(connection, transaction, table, enabled: true);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task SetConstraintsEnabledAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        bool enabled)
    {
        var sql = enabled
            ? $"ALTER TABLE dbo.[{table}] WITH CHECK CHECK CONSTRAINT ALL"
            : $"ALTER TABLE dbo.[{table}] NOCHECK CONSTRAINT ALL";

        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertRowAsync(SqlConnection connection, SqlTransaction transaction, string table, Dictionary<string, JsonElement> row)
    {
        var columns = row.Keys.ToArray();
        var sql = $"INSERT INTO dbo.[{table}] ({string.Join(',', columns.Select(x => $"[{x}]"))}) VALUES ({string.Join(',', columns.Select((_, i) => $"@p{i}"))})";
        await using var command = new SqlCommand(sql, connection, transaction);
        for (var i = 0; i < columns.Length; i++) command.Parameters.AddWithValue($"@p{i}", ConvertElement(row[columns[i]]) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static object? ConvertElement(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String when value.TryGetDateTime(out var date) => date,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt32(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.ToString()
    };

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string table, SqlTransaction? transaction = null)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@name,'U') IS NULL THEN 0 ELSE 1 END", connection, transaction);
        command.Parameters.AddWithValue("@name", $"dbo.{table}");
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> HasIdentityAsync(SqlConnection connection, string table, SqlTransaction transaction)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.identity_columns WHERE object_id=OBJECT_ID(@name)) THEN 1 ELSE 0 END", connection, transaction);
        command.Parameters.AddWithValue("@name", $"dbo.{table}");
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static string Escape(object? value)
    {
        var text = Convert.ToString(value) ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    public sealed class DatabaseSnapshot
    {
        public string Format { get; set; } = "FinanceManagerAspNet.Backup.v1";
        public DateTime CreatedAtUtc { get; set; }
        public Dictionary<string, List<Dictionary<string, JsonElement>>> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
