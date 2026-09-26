# Phase 8.4.1 Batch 1 — Allocation Pot-Link Data Foundation

## Scope

This batch introduces the data foundation required for dashboard Household Reserve allocations to select an existing reserve pot in a later UI batch.

No dashboard form behaviour changes in this batch.

## Database changes

The application-managed `dbo.savings` table now includes:

- `reserve_pot_id int NULL`
- `pot_name_snapshot nvarchar(140) NULL`
- Foreign key `FK_savings_reserve_pots` with `ON DELETE SET NULL`
- Index `IX_savings_reserve_pot_id`

`reserve_pot_id` is the authoritative relationship for linked allocations. `pot_name_snapshot` preserves the allocation's original destination label for historical display and unmatched legacy records.

## Historical backfill

On startup, existing allocation rows are handled safely:

1. Missing snapshots are populated from the existing savings name.
2. Names are compared after trimming and case normalisation.
3. An allocation is linked only when exactly one reserve pot has the matching normalised name.
4. Ambiguous or unmatched allocations remain unlinked.
5. No new reserve pots are created by the backfill.

The backfill is idempotent and only fills `reserve_pot_id` when it is currently null.

## Model and repository changes

`PaymentRow` now exposes:

- `ReservePotId`
- `PotNameSnapshot`
- `CurrentReservePotName`
- `DisplayName`

Repository reads join `dbo.savings` to `dbo.reserve_pots`, allowing the UI to use the current pot name while retaining the original snapshot.

Repository groundwork added for later batches:

- `GetReservePotByIdAsync`
- `GetSelectableReservePotsAsync`
- `FindReservePotByNormalisedNameAsync`

New savings rows capture `pot_name_snapshot`. Existing creation behaviour remains unchanged until the service and UI batches introduce explicit destination modes.

## Compatibility

Historical rows without a valid linked pot continue to load and display using their stored name/snapshot. Deleting a reserve pot clears the link rather than deleting allocation history.

## Next batch

Phase 8.4.1 Batch 2 will add the allocation request model and service workflow for:

- Existing-pot allocations
- New-pot creation
- Exact duplicate detection
- Transactional balance updates
