# Phase 10.3.1 — Backup Restore Foreign-Key Fix

## Problem

Restoring a JSON backup could insert child rows such as `savings` before their referenced `reserve_pots` rows existed. SQL Server then rejected the restore with `FK_savings_reserve_pots`.

## Resolution

- The restore operation now discovers the backed-up tables that exist in the current database.
- Foreign-key checks are temporarily suspended only inside the restore transaction.
- Existing rows are removed and the snapshot is restored with its original identity values.
- Every constraint is re-enabled using `WITH CHECK CHECK`, which validates all restored relationships before the transaction can commit.
- Any invalid or incomplete backup still rolls back in full.
- `IDENTITY_INSERT` is always switched off through a `finally` block, including when an insert fails.

This fixes parent/child restore ordering without weakening database integrity.
