using FinanceManagerAspNet.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FinanceManagerAspNet.Services;

public sealed partial class FinanceRepository
{
    public async Task<List<PrepareNextMonthSection>> GetMonthPreparationSectionsAsync(int year, int month)
    {
        var next = new DateTime(year, month, 1).AddMonths(1);
        var income = await GetMonthlyIncomeEntriesAsync(year, month);
        var nextIncome = await GetMonthlyIncomeEntriesAsync(next.Year, next.Month);
        var sections = new List<PrepareNextMonthSection>
        {
            new()
            {
                Source = "income", Title = "Income",
                Items = MonthPreparationItem.Missing(income.Select(x => new MonthPreparationItem(
                    "income", x.Id, x.Name, x.Amount, x.Date, x.IsRecurring, x.Category, Notes: x.Notes)),
                    nextIncome.Select(x => x.Name))
            }
        };
        var pots = await GetReservePotsAsync();
        foreach (var (source, title) in new[] {
            ("bills", "Essential bills"), ("everyday_spending", "Everyday spending"),
            ("investments", "Investments"), ("savings", "Money Pots"), ("extra_expenses", "Extra expenses") })
        {
            var previous = await GetRowsAsync(source, month, year);
            var existing = await GetRowsAsync(source, next.Month, next.Year);
            var recurring = source == "extra_expenses" ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : (await GetMonthlyEntryTemplatesAsync(source)).Select(x => x.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<MonthPreparationItem>();
            foreach (var row in previous)
            {
                ReservePot? pot = null;
                if (source == "savings")
                {
                    // Only real, eligible pot contributions can be replicated. Managed
                    // Emergency Fund movements and withdrawals need their own workflows.
                    pot = row.ReservePotId.HasValue ? pots.FirstOrDefault(x => x.Id == row.ReservePotId)
                        : pots.FirstOrDefault(x => string.Equals(x.Name.Trim(), row.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (pot is null || !pot.IsActive || pot.IsPausedFor(next) || row.Amount < 0) continue;
                }
                candidates.Add(new MonthPreparationItem(source, row.Id, pot?.Name ?? row.Name,
                    row.Amount, row.Date, recurring.Contains(row.Name.Trim()), row.Category,
                    row.Type, row.Length, row.Notes, row.AccountBalanceId, pot?.Id));
            }
            sections.Add(new PrepareNextMonthSection { Source = source, Title = title,
                Items = MonthPreparationItem.Missing(candidates, existing.Select(x => x.DisplayName)) });
        }
        return sections;
    }

    public async Task<int> PrepareSelectedMonthEntriesAsync(int year, int month, IReadOnlyCollection<string> selectedKeys)
    {
        if (selectedKeys.Count == 0) return 0;
        var next = new DateTime(year, month, 1).AddMonths(1);
        var selected = selectedKeys.ToHashSet(StringComparer.Ordinal);
        // Re-read eligibility and values; the browser supplies identifiers only.
        var items = (await GetMonthPreparationSectionsAsync(year, month)).SelectMany(x => x.Items)
            .Where(x => selected.Contains(x.Key)).OrderBy(x => x.Source).ThenBy(x => x.Id).ToList();
        await using var con = new SqlConnection(ConnStr);
        await con.OpenAsync();
        await using var transaction = await con.BeginTransactionAsync(IsolationLevel.Serializable);
        var tx = (SqlTransaction)transaction;
        var added = 0;
        var updatedPots = new Dictionary<int, string>();
        var incomeChanged = false;
        foreach (var item in items)
        {
            var (table, dateColumn, columns, values) = item.Source switch
            {
                "income" => ("monthly_income_entries", "[date]", "category,notes,is_recurring", "@category,@notes,@recurring"),
                "bills" => ("bills", "[date]", "[type],[length],[description],account_balance_id", "@type,@length,@notes,@account"),
                "everyday_spending" => ("everyday_spending", "[date]", "category,[type],[length],[description],account_balance_id", "@category,@type,@length,@notes,@account"),
                "extra_expenses" => ("extra_expenses", "duedate", "category,[type],[length],[description],account_balance_id", "@category,@type,@length,@notes,@account"),
                "investments" => ("investments", "[date]", "category,[type],[length],notes,account_balance_id", "@category,@type,@length,@notes,@account"),
                "savings" => ("savings", "[date]", "[type],[length],notes,account_balance_id,reserve_pot_id,pot_name_snapshot", "@type,@length,@notes,@account,@pot,@name"),
                _ => throw new InvalidOperationException("Unknown preparation section.")
            };
            var potGuard = item.Source == "savings" ? @"
AND EXISTS (SELECT 1 FROM dbo.reserve_pots WITH (UPDLOCK,HOLDLOCK)
 WHERE reserve_pot_id=@pot AND is_active=1
 AND (funding_paused_until IS NULL OR funding_paused_until<@start
      OR COALESCE(funding_paused_from,CONVERT(date,GETDATE()))>@start))" : "";
            await using var cmd = new SqlCommand($@"
IF NOT EXISTS (SELECT 1 FROM dbo.{table} WITH (UPDLOCK,HOLDLOCK)
 WHERE {dateColumn}>=@start AND {dateColumn}<@end
 AND (LOWER(LTRIM(RTRIM([name])))=LOWER(LTRIM(RTRIM(@name)))
 {(item.Source == "savings" ? "OR reserve_pot_id=@pot" : "")})) {potGuard}
BEGIN
 INSERT INTO dbo.{table}([name],amount,{dateColumn},{columns}) VALUES(@name,@amount,@date,{values});
 SELECT 1;
END
ELSE SELECT 0;", con, tx);
            cmd.Parameters.AddWithValue("@name", item.Name.Trim());
            cmd.Parameters.AddWithValue("@amount", item.Amount);
            cmd.Parameters.AddWithValue("@date", item.DateIn(next));
            cmd.Parameters.AddWithValue("@start", next);
            cmd.Parameters.AddWithValue("@end", next.AddMonths(1));
            cmd.Parameters.AddWithValue("@category", DbValue(item.Category));
            cmd.Parameters.AddWithValue("@type", DbValue(item.Type));
            cmd.Parameters.AddWithValue("@length", DbValue(item.Length));
            cmd.Parameters.AddWithValue("@notes", DbValue(item.Notes));
            cmd.Parameters.AddWithValue("@account", (object?)item.AccountBalanceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pot", (object?)item.ReservePotId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recurring", item.IsRecurring);
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0) continue;
            added++;
            incomeChanged |= item.Source == "income";
            if (item.ReservePotId is int potId && item.Amount > 0)
            {
                await using var allocation = new SqlCommand(@"
UPDATE dbo.reserve_pots SET allocated_amount=CASE WHEN allocated_amount+@amount<0 THEN 0 ELSE allocated_amount+@amount END,updated_at=SYSUTCDATETIME() WHERE reserve_pot_id=@pot;
UPDATE dbo.household_reserve SET balance=CASE WHEN balance+@amount<0 THEN 0 ELSE balance+@amount END,updated_at=SYSUTCDATETIME() WHERE household_reserve_id=1;
INSERT INTO dbo.finance_events(area,event_type,entity_type,entity_id,title,[description],amount,source)
VALUES('Household Reserve','ContributionAdded','ReservePot',@pot,@title,'Contribution recorded through Prepare next month.',@amount,'Dashboard');", con, tx);
                allocation.Parameters.AddWithValue("@pot", potId);
                allocation.Parameters.AddWithValue("@amount", item.Amount);
                allocation.Parameters.AddWithValue("@title", $"{item.Name} funded");
                await allocation.ExecuteNonQueryAsync();
                updatedPots[potId] = item.Name;
            }
        }
        if (incomeChanged)
        {
            await using var sync = new SqlCommand(@"
DECLARE @total decimal(18,2)=(SELECT COALESCE(SUM(amount),0) FROM dbo.monthly_income_entries WHERE [date]>=@start AND [date]<@end);
MERGE dbo.monthly_income_stats WITH (HOLDLOCK) AS target USING (SELECT @year [year],@month [month]) AS source
ON target.[year]=source.[year] AND target.[month]=source.[month]
WHEN MATCHED THEN UPDATE SET amount=@total,updated_at=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT([year],[month],amount,sick_days) VALUES(@year,@month,@total,0);", con, tx);
            sync.Parameters.AddWithValue("@year", next.Year);
            sync.Parameters.AddWithValue("@month", next.Month);
            sync.Parameters.AddWithValue("@start", next);
            sync.Parameters.AddWithValue("@end", next.AddMonths(1));
            await sync.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        foreach (var (potId, name) in updatedPots)
        {
            await SyncReservePotContributionAverageAsync(name);
            await RebuildReservePotFundingHistoryAsync(potId);
        }
        return added;
    }
}
