# Phase 11.6.2 - Flexible Interest Handling

## Implemented

- Every account now has a default interest-handling preference:
  - Keep invested
  - Add to Monthly Income
- The Emergency Fund stores the same preference through application settings.
- When monthly interest is confirmed, the saved account preference is pre-selected but can be overridden for that individual payment.
- A third monthly option, **Ignore for now**, leaves the estimate unconfirmed and creates neither an income entry nor a reconciliation record.
- **Keep invested** records the received interest for account reconciliation without adding it to monthly available income.
- **Add to Monthly Income** records the interest for reconciliation and creates the corresponding monthly income entry.
- Account Management displays the saved preference, and recorded interest shows how it was handled.
- Existing passive-income records are migrated as **Add to Monthly Income** to preserve their historic meaning.

## Data changes

- `account_balances.interest_handling`
- `passive_income_records.interest_handling`
- `EmergencyFundInterestHandling` finance setting

Schema changes are applied by the existing automatic modern-table bootstrap.

## Phase 11.6.4 refinement

Pending interest handling can be corrected before it is finalised. The choice becomes locked after the payment is added to monthly income or reconciled into an account balance.
