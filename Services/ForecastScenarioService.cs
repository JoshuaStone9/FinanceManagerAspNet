using FinanceManagerAspNet.Models;
using Microsoft.Data.SqlClient;

namespace FinanceManagerAspNet.Services;

public interface IForecastScenarioService
{
    Task<IReadOnlyList<SavedForecastScenario>> GetAllAsync();
    Task<SavedForecastScenario?> GetAsync(int id);
    Task<int> SaveAsync(string name, WhatIfForecastInput input);
    Task RenameAsync(int id, string name);
    Task<int?> DuplicateAsync(int id);
    Task DeleteAsync(int id);
    Task SetPreferredAsync(int id);
    Task<SavedForecastScenario?> GetPreferredAsync();
}

public sealed class ForecastScenarioService(IConfiguration configuration) : IForecastScenarioService
{
    private string ConnectionString => Environment.GetEnvironmentVariable("FM_CONNECTION_STRING")
        ?? configuration.GetConnectionString("FinanceManager")
        ?? throw new InvalidOperationException("Missing FinanceManager connection string.");

    public async Task<IReadOnlyList<SavedForecastScenario>> GetAllAsync()
    {
        const string sql = "SELECT * FROM dbo.forecast_scenarios ORDER BY is_preferred DESC, updated_at DESC, [name]";
        var results = new List<SavedForecastScenario>();
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(Read(reader));
        return results;
    }

    public async Task<SavedForecastScenario?> GetAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.forecast_scenarios WHERE forecast_scenario_id=@id";
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<int> SaveAsync(string name, WhatIfForecastInput input)
    {
        const string sql = @"INSERT INTO dbo.forecast_scenarios
([name],months,pot_id,monthly_contribution_override,target_amount_override,target_date_override,one_off_contribution,interest_rate_override,protected_baseline_override,future_expense_amount,future_expense_date)
OUTPUT INSERTED.forecast_scenario_id
VALUES(@name,@months,@potId,@monthly,@target,@targetDate,@oneOff,@interest,@baseline,@expense,@expenseDate)";
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        AddInputParameters(command, string.IsNullOrWhiteSpace(name) ? "Untitled scenario" : name.Trim(), input);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public Task RenameAsync(int id, string name) => ExecuteAsync(
        "UPDATE dbo.forecast_scenarios SET [name]=@name, updated_at=SYSUTCDATETIME() WHERE forecast_scenario_id=@id",
        ("@id", id), ("@name", string.IsNullOrWhiteSpace(name) ? "Untitled scenario" : name.Trim()));

    public async Task<int?> DuplicateAsync(int id)
    {
        var source = await GetAsync(id);
        if (source is null) return null;
        return await SaveAsync($"{source.Name} copy", source.ToInput());
    }

    public Task DeleteAsync(int id) => ExecuteAsync(
        "DELETE FROM dbo.forecast_scenarios WHERE forecast_scenario_id=@id", ("@id", id));

    public async Task SetPreferredAsync(int id)
    {
        const string sql = @"BEGIN TRANSACTION;
UPDATE dbo.forecast_scenarios SET is_preferred=0 WHERE is_preferred=1;
UPDATE dbo.forecast_scenarios SET is_preferred=1, updated_at=SYSUTCDATETIME() WHERE forecast_scenario_id=@id;
COMMIT;";
        await ExecuteAsync(sql, ("@id", id));
    }

    public async Task<SavedForecastScenario?> GetPreferredAsync()
    {
        const string sql = "SELECT TOP 1 * FROM dbo.forecast_scenarios WHERE is_preferred=1 ORDER BY updated_at DESC";
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddInputParameters(SqlCommand command, string name, WhatIfForecastInput input)
    {
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@months", new[] { 3, 6, 12, 24 }.Contains(input.Months) ? input.Months : 12);
        command.Parameters.AddWithValue("@potId", (object?)input.PotId ?? DBNull.Value);
        command.Parameters.AddWithValue("@monthly", (object?)input.MonthlyContributionOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@target", (object?)input.TargetAmountOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@targetDate", (object?)input.TargetDateOverride?.Date ?? DBNull.Value);
        command.Parameters.AddWithValue("@oneOff", Math.Max(0m, input.OneOffContribution));
        command.Parameters.AddWithValue("@interest", (object?)input.InterestRateOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@baseline", (object?)input.ProtectedBaselineOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@expense", Math.Max(0m, input.FutureExpenseAmount));
        command.Parameters.AddWithValue("@expenseDate", (object?)input.FutureExpenseDate?.Date ?? DBNull.Value);
    }

    private static SavedForecastScenario Read(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("forecast_scenario_id")),
        Name = reader.GetString(reader.GetOrdinal("name")),
        IsPreferred = reader.GetBoolean(reader.GetOrdinal("is_preferred")),
        Months = reader.GetInt32(reader.GetOrdinal("months")),
        PotId = reader.IsDBNull(reader.GetOrdinal("pot_id")) ? null : reader.GetInt32(reader.GetOrdinal("pot_id")),
        MonthlyContributionOverride = GetNullableDecimal(reader, "monthly_contribution_override"),
        TargetAmountOverride = GetNullableDecimal(reader, "target_amount_override"),
        TargetDateOverride = GetNullableDate(reader, "target_date_override"),
        OneOffContribution = reader.GetDecimal(reader.GetOrdinal("one_off_contribution")),
        InterestRateOverride = GetNullableDecimal(reader, "interest_rate_override"),
        ProtectedBaselineOverride = GetNullableDecimal(reader, "protected_baseline_override"),
        FutureExpenseAmount = reader.GetDecimal(reader.GetOrdinal("future_expense_amount")),
        FutureExpenseDate = GetNullableDate(reader, "future_expense_date"),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
    };

    private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static DateTime? GetNullableDate(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
