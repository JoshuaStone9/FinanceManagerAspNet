# Phase 14.0 – Account Linking for Statement Reconciliation

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
