# V2 Stage 1 - Pot Funding Plan

This stage adds planning settings to Household Reserve virtual pots while keeping the dashboard-calculated monthly average separate.

Added settings:
- Intended contribution amount
- Funding frequency: Monthly, Weekly, or Irregular
- Optional expected funding day (1-31)
- Carry missed funding forward preference
- Optional pause-until date
- Optional pause reason

Database behaviour:
- Existing `reserve_pots` tables are upgraded automatically through `EnsureModernTablesAsync`.
- Existing rows receive safe defaults: intended contribution £0, Monthly frequency, and carry-forward enabled.

Not included yet:
- Individual contribution history
- Monthly funding records
- Missed/partial/excess calculations
- Recovery allocations
- Events page
