# Phase 15.2 — Unified month setup

## Status

Proposed.

## Goal

Replace global preparation, per-workspace setup panels and repeated recurring-entry prompts with one controlled workflow for creating a month.

## Workflow

1. Select the target month.
2. Load eligible recurring income, bills, everyday spending, investment contributions and pot funding.
3. Mark entries already present and prevent duplicate creation.
4. Allow entries to be included or skipped without changing their recurring status.
5. Allow proposed amounts and dates to be adjusted before confirmation.
6. Preview income, spending, future allocations and expected remainder.
7. Confirm the selected entries once and show a result summary.

Extra expenses remain one-off by default and are not proposed unless a later explicit policy is introduced.

## Data and safety rules

- Setup is idempotent: submitting the same selection twice creates no duplicates.
- Existing target-month entries are never overwritten.
- The source entry or template retains its recurrence setting when skipped.
- Account assignments, notes and day-of-month values are preserved where applicable.
- Validation occurs before any selected entries are written.
- A partial failure must not leave an unexplained mixed result; use a transaction where the data-access design permits it.

## Acceptance criteria

- One setup action covers all supported recurring monthly categories.
- Per-workspace setup panels can be removed without losing capability.
- The preview totals use the same calculation rules as Month Overview.
- Repeating a successful request does not create duplicates.
- Tests cover existing entries, skipped entries, adjusted proposals and repeated submissions.

## Out of scope

- Automatically carrying a previous month's surplus or shortfall.
- Changing pot funding recommendations.
- Scheduling unattended month creation.
