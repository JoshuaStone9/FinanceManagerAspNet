# Phase 15.1 — Unified Month workspace

## Status

Proposed.

## Goal

Create one obvious place to understand and manage a selected month. The existing monthly category workspaces become tabs within this destination rather than separate sidebar concepts.

## Workspace structure

The Month destination provides:

- A persistent previous/current/next month selector.
- A compact summary of income, core spending, future allocations and remaining money.
- Month setup status and the primary setup action.
- Tabs for Overview, Income, Essential bills, Everyday spending, Extra expenses, Investment contributions and Pot funding.
- Starting-adjustment visibility on Overview, with editing delivered before the legacy screen is removed.

Existing category views may continue to render the tab contents. This phase does not require one large Razor view or a single controller action.

## Behaviour

- The selected year and month persist between tabs and after every write.
- Month navigation retains the active tab where possible.
- Each entry type has one primary add, edit and delete flow.
- Read-only mode shows the same information but suppresses write controls.
- Direct links to existing monthly actions continue to work.

## Acceptance criteria

- The sidebar contains one Month entry rather than six monthly-category entries.
- A user can reach every existing monthly workspace from Month.
- Overview totals match the existing dashboard and workspace calculations.
- Existing add, edit, delete and recurring behaviour remains functional.
- Mobile users can switch tabs without horizontal page overflow or inaccessible controls.

## Out of scope

- Replacing month preparation; that belongs to Phase 15.2.
- Deleting Manage Month; that belongs to Phase 15.3.
- Changing budget formulas or automatic allocation behaviour.
