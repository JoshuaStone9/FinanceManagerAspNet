# Phase 16.1 — Pots hub

## Status

Proposed. Depends on Phase 15's unified Month workspace.

## Goal

Replace the separate Money Pots, monthly pot funding and Pot Activity destinations with one hub whose tabs explain their relationship.

## Tabs

- **Goals** — pot balances, targets, dates, priorities, pauses and configuration.
- **Funding** — monthly pot contributions for the selected month.
- **Activity** — contribution, withdrawal, recovery and recommendation history.

## Financial boundaries

- Pot allocations remain virtual subdivisions of the selected reserve accounts.
- The hub must always show the distinction between available household reserve and allocated pot totals.
- Editing a monthly funding entry must use the same canonical flow as Month → Pot funding.
- Goal edits must not rewrite immutable funding history.

## Acceptance criteria

- Only one Pots destination appears in primary navigation.
- Month → Pot funding and Pots → Funding display the same underlying records and actions.
- Goals, funding and activity retain independent filters without losing the selected pot where applicable.
- Negative balances, paused pots and funding shortfalls remain clearly visible.
- Existing Money Pots and Pot Activity URLs remain compatible.

## Out of scope

- Merging virtual pot balances with bank-account balances.
- Recalculating historical allocations solely for the new presentation.
