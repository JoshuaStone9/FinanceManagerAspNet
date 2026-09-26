# Phase 10.10 — Account Reconciliation

> Historical reconciliation record. The Phase 10 overview describes a different manual balance-check variant. These accounts are retained as historical records; the [Phase 11 guide](../phase-11/README.md) links the later reconciliation changes.

Implemented a dedicated reconciliation workspace for interest that has been recorded as monthly income but not yet added to the physical source-account balance. The dashboard now separates operating income, passive income and total available income. Statistics warns about unreconciled interest and stale account balances. Reconciliation history participates in backup, restore and factory reset through the `account_reconciliations` table.
