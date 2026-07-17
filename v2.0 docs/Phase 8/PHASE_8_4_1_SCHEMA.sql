-- Phase 8.4.1 Batch 1 reference migration.
-- The application applies this idempotently from FinanceRepository.EnsureModernTablesAsync.

IF COL_LENGTH('dbo.savings','reserve_pot_id') IS NULL
    ALTER TABLE dbo.savings ADD reserve_pot_id int NULL;

IF COL_LENGTH('dbo.savings','pot_name_snapshot') IS NULL
    ALTER TABLE dbo.savings ADD pot_name_snapshot nvarchar(140) NULL;

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

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name='IX_savings_reserve_pot_id'
      AND object_id=OBJECT_ID('dbo.savings'))
    CREATE INDEX IX_savings_reserve_pot_id ON dbo.savings(reserve_pot_id);
