# Phase 6.5 – Withdrawal Funding Reconciliation

## Change

Withdrawals are now included when rebuilding monthly funding history.

- Same-month withdrawals reduce that month’s recorded contribution.
- Any remaining withdrawal consumes carried excess from earlier months.
- Withdrawals cannot create funding or carried excess.
- Pot status is derived from the actual pot balance and target, while monthly funding status remains separate.
- Funding history is rebuilt immediately after a withdrawal.
