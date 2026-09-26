# Phase 11.9 — Event Exports and Emergency Fund Restoration

## Status

Implemented.

## Purpose

Phase 11.9 improves the financial audit trail and provides a correct workflow for rebuilding the Emergency Fund when it is below its configured base level.

The Emergency Fund is part of the Household Reserve. Money added to it remains owned and must therefore be recorded as a contribution or allocation, not as an Extra Expense.

## Event exports

The Events page now provides one-click exports using the filters currently selected on the page.

### CSV

CSV is the primary analysis format. It contains:

- Occurred At
- Area
- Event Type
- Entity Type
- Entity ID
- Title
- Description
- Amount
- Direction
- Source

Direction is derived from the amount: positive amounts are `In`, negative amounts are `Out`, and non-monetary events are blank.

### PDF

PDF provides a readable chronological report. It contains the selected filter summary, generation date, event count, event details, amounts and source information.

Exports respect:

- Money-pot/allocation filter
- Area filter
- Event-type filter
- From and To dates
- Detailed audit visibility

## Emergency Fund restoration

A dedicated form is available on Account Management for adding money to the Emergency Fund.

### Business rules

1. The contribution must be greater than zero.
2. The amount is added to the existing Emergency Fund balance.
3. The contribution is not added to Extra Expenses.
4. The operation records an `EmergencyFundContribution` event.
5. The event records the contribution amount, previous balance, new balance and remaining shortfall.
6. Other money pots remain available while the Emergency Fund is below its base level.
7. The shortfall message is advisory and does not block other allocations.
8. Reaching or exceeding the base level is clearly confirmed.

## Event taxonomy

Phase 11.9 uses the existing structured event system. Recommended financial event types include:

- `EmergencyFundContribution`
- `EmergencyFundWithdrawal`
- `EmergencyFundAdjustment`
- `ContributionAdded`
- `ContributionReversed`
- `PotWithdrawal`
- `PotOverdrawn`
- `Transfer`
- `InterestApplied`
- `BalanceCorrection`
- `AuditAction`

Free-text descriptions supplement these types but do not replace them.

## Technical changes

- Added `FinanceEventExportService`.
- Added `EventsController.ExportCsv`.
- Added `EventsController.ExportPdf`.
- Added `FinanceRepository.AddEmergencyFundContributionAsync`.
- Added `ReconciliationController.AddEmergencyFundContribution`.
- Added export controls to the Events view.
- Added the Emergency Fund restoration panel to Account Management.

## Database changes

No migration is required. The implementation uses the existing:

- `emergency_fund`
- `finance_settings`
- `finance_events`

The Emergency Fund balance update and event insert are performed in one SQL transaction.

## Future extensions

- Add a dedicated Emergency Fund withdrawal action.
- Add transfer source-account selection.
- Add event metadata as structured JSON.
- Add acknowledged/resolved discrepancy events.
- Add scheduled monthly export packs.
- Add Excel export if richer workbook formatting becomes useful.


## Dashboard allocation integration fix

Emergency-fund restoration contributions are also written to `dbo.savings` for the current date so they appear in the Dashboard's Money Pot/Future Allocations section and are included in the month's allocation total.

The dashboard row uses the type `Emergency Fund Restoration`. It does **not** call `ApplyReserveAllocationAsync`, because doing so would create or increase a separate virtual reserve pot and double-count the same money. These generated rows are shown as managed by Account Management rather than directly editable from the Dashboard.

## Emergency Fund transaction ledger and reversals

Emergency Fund movements are now represented by individual ledger transactions in `dbo.emergency_fund_transactions`.

Each new contribution stores:

- its own transaction ID;
- the contribution amount and optional note;
- the linked Dashboard `savings` row;
- the linked `finance_events` audit event;
- whether it has been reversed and which reversal transaction performed it.

A user may make any number of contributions. Each remains independently reversible. Reversing a contribution:

1. locks and validates the selected transaction;
2. rejects a second reversal of the same transaction;
3. subtracts only that contribution from the Emergency Fund balance;
4. removes only its linked Dashboard allocation;
5. preserves the original finance event;
6. creates an `EmergencyFundContributionReversed` finance event with a negative amount;
7. creates a separate reversal ledger transaction and links both transactions;
8. commits all changes in one SQL transaction.

The system does not provide a generic “undo last contribution” action because that could affect the wrong entry when several contributions exist.

Contributions created before the ledger table was introduced remain visible in Finance Events and on historic Dashboard months, but they are not automatically backfilled into the ledger because their exact event-to-dashboard linkage cannot be proven safely.

## Collapsible restoration interface

The Account Management page keeps Phase 11.9 unobtrusive:

- The entire Restore Emergency Fund feature is shown only while the recorded Emergency Fund balance is below the configured base reserve level.
- A compact top-level control opens and closes the contribution form.
- The transaction ledger is collapsed independently under its own Show/Hide control.
- Once the base level has been reached, the restoration control, contribution form, and ledger panel are not rendered.
- The ledger data is retained even when hidden so that it becomes available again if the Emergency Fund later falls below its base level.
