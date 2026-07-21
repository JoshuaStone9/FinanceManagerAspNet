# Phase 11.8 - Combined Household Reserve Reconciliation

The Accounts page reconciliation is a combined Household Reserve check rather than an individual account baseline check.

## Calculation

- **Actual total:** combined current balances of the accounts selected for the Household Reserve.
- **Expected total:** `EmergencyFundBaseline` from Settings plus the current `allocated_amount` of every active virtual reserve pot.
- **Difference:** actual total minus expected total.

A positive difference means there is more money in the selected real accounts than the base reserve and virtual pots explain. A negative difference means there is less. Differences within `AccountBalanceTolerance` are treated as likely interest or rounding variance.

Individual account cards show their current balance and whether they are included in the combined check. The separate `starting_balance` and `starting_amount` fields do not drive reconciliation.
