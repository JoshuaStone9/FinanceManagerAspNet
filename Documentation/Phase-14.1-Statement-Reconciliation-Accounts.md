# Phase 14.1 — Statement Reconciliation Accounts

## Summary

Phase 14.1 separates balance-tracked savings and investment accounts from accounts used primarily for monthly PDF statement inspection. It builds on Phase 14.0 account linking without implementing PDF extraction or transaction matching yet.

## Account usage modes

Each account now has one of three usage modes:

- **Tracking** — retains balances, interest rates, contributions, tax treatment and forecast behaviour.
- **StatementOnly** — identifies the account used by linked entries and future statement imports, but does not participate in balances, interest, Household Reserve calculations or forecasts.
- **Both** — supports full balance tracking and future statement reconciliation.

Existing accounts default to `Tracking`, preserving current behaviour.

## Statement metadata

Statement-capable accounts can store:

- optional final four account digits;
- a statement parser preference;
- active/inactive status.

The parser preference currently records configuration only. PDF extraction is intentionally deferred.

## User interface

The Account Management page now presents separate areas for:

1. tracked savings and investments;
2. statement reconciliation accounts.

The create and edit forms dynamically show only settings relevant to the selected usage mode. Statement-only accounts do not request opening balances, interest, tax or forecast settings.

## Database changes

The application startup schema check adds nullable/backwards-compatible columns to `dbo.account_balances`:

```sql
usage_type nvarchar(30) NOT NULL DEFAULT 'Tracking'
last_four_digits nvarchar(4) NULL
statement_parser nvarchar(80) NOT NULL DEFAULT 'Generic'
is_active bit NOT NULL DEFAULT 1
```

No existing account is converted automatically to statement-only.

## Business rules

- Statement-only accounts are forced to zero balance, zero interest and zero monthly contribution.
- Statement-only accounts are excluded from Statistics forecasts.
- Statement-only accounts cannot be the default Emergency Fund destination.
- Household Reserve totals include balance-tracked accounts only.
- `Both` accounts continue to behave as tracked accounts and also appear in the statement area.
- Linked monthly entries can use any active account, including statement-only accounts.

## Out of scope

Phase 14.1 does not include:

- PDF upload;
- PDF/OCR extraction;
- statement transaction storage;
- matching scores;
- create/edit actions from unmatched statement lines;
- statement reconciliation completion history.

## Acceptance criteria

- Existing accounts load as tracking accounts.
- A statement-only account can be created without financial tracking values.
- Statement-only accounts do not change reserve or forecast totals.
- A combined account supports both behaviours.
- Usage mode, final four digits, parser preference and active status can be edited.
- Account-linked monthly entries remain backwards compatible.

## Next stage

Phase 14.2 should introduce statement upload records, secure PDF storage, extracted statement metadata and a preview workflow before transaction matching is enabled.
