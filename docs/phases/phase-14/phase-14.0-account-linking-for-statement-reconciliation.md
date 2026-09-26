# Phase 14.0 – Account Linking for Statement Reconciliation

> The Future Roadmap section records the original proposal. The later [Phase 14.2 workspace](phase-14.2-statement-reconciliation-workspace.md) delivered manual matching before PDF extraction. See the [phase guide](README.md) for that revised sequence.

## Executive Summary
This phase introduces optional account linking for financial entries. The goal is to establish a relationship between recorded transactions and financial accounts without changing existing budgeting behaviour. This provides the foundation for future PDF statement reconciliation.

## Objectives
- Allow expenses and allocations to be associated with an account.
- Preserve backwards compatibility.
- Prepare for PDF statement matching.

## Scope
Included:
- Optional account selection.
- Essential Bills.
- Everyday Spending.
- Extra Expenses.
- Investments.
- Money Pot contributions.
- Carry-forward preserves account assignment.

Out of scope:
- Income account assignment.
- PDF import.
- OCR.
- Automatic matching.
- Automatic balance updates.

## Database
A nullable AccountBalanceId (or equivalent FK) is added to supported entities so existing records remain valid.

## User Experience
Users may optionally assign an account when creating or editing entries. Existing records continue to function without an assigned account.

## Business Rules
- Account assignment is optional.
- Existing data requires no migration.
- Recurring entries retain the assigned account.

## Future Roadmap
Phase 14.1 – Statement Reconciliation Accounts.
Phase 14.2 – PDF Statement Import.
Phase 14.3 – Matching Engine.
Phase 14.4 – Reconciliation Workspace.

## Acceptance Criteria
- Entries can be linked to accounts.
- Links persist after editing.
- Links persist during carry-forward.
- Existing entries remain valid.

## Implementation details

Consolidated from the separate monthly-entry account-links note.

Implemented optional account assignment for essential bills, everyday spending, extra expenses, investments and money-pot contributions.

### Behaviour
- Each monthly quick-entry form now includes an optional Account dropdown.
- Existing entries can be assigned or changed from the Edit screen.
- Assigned account names appear underneath recorded entries.
- Existing records remain valid because account assignment is nullable.
- Direct month carry-over retains the assigned account where applicable.
- Startup database maintenance adds `account_balance_id` to the five monthly payment tables automatically.

### Scope
Income account assignment and PDF statement parsing are not included in this phase. The payment-account link is the prerequisite for later statement matching.
