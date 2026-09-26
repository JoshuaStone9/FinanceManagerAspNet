# Phase 8.5 — Monthly Income Workspace

Finance Manager now records actual monthly income as individual sources rather than relying only on a fixed setting.

## Included

- Income is the first Monthly Money workspace.
- Multiple sources per month: salary, overtime, bonus, benefits, refunds and other income.
- Current-day default within the selected month.
- Recurring and one-off income sources.
- Prepare-month support using recurring sources from the immediately preceding month.
- Add, edit, delete and recurrence controls.
- Dashboard and Phase 9.1 financial health use the summed monthly income entries whenever they exist.
- The existing monthly income aggregate remains synchronised for backwards compatibility and statistics.

The table `dbo.monthly_income_entries` is created automatically by the repository table bootstrap; no manual migration is required.
