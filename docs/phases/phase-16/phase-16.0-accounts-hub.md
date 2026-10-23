# Phase 16.0 — Accounts hub

## Status

Proposed. Depends on the Phase 15 navigation foundation.

## Goal

Give account-oriented work one destination while keeping balance management, statement reconciliation and holding valuation conceptually distinct.

## Tabs

- **Balances** — account purposes, actual balances, reconciliation settings, interest handling and Emergency Fund configuration.
- **Statements** — statement periods, upload/import status and reconciliation workspaces.
- **Holdings** — investment and other asset holdings with current valuations.

Monthly investment contributions remain in Month and link to Holdings only for context.

## Shared context

- Account selection should persist when moving from Balances to Statements.
- Statement status and unresolved counts may be summarised on the hub landing page.
- Holdings use their own valuation timestamps and must not be presented as statement-reconciled cash balances.

## Acceptance criteria

- Accounts and Statements are no longer separate primary sidebar entries.
- All existing account, statement and holding actions remain reachable.
- Statement workflows retain the selected period and account.
- Labels distinguish cash balances, protected reserve, holdings and monthly contributions.
- Existing specialist URLs remain compatible.

## Out of scope

- Combining cash accounts and asset holdings in one database model.
- Automatic bank connections.
- Changing statement matching rules.
