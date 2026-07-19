# Phase 11.7 — Tax-aware interest engine

## Implemented

- Calculates monthly gross interest from the recorded account balance and AER.
- Applies the configured account tax rate when the account is taxable.
- Treats a blank tax start date as effective immediately.
- Honours future tax-effective dates.
- Stores estimated and actual gross, tax and net values for confirmed interest.
- Uses net interest for Monthly Income entries.
- Uses net interest for pending account reconciliation and suggested balances.
- Shows gross, estimated tax and net interest in the Income workspace.
- Makes Statistics projections tax-aware.
- Shows projected gross interest, projected tax and projected net interest.
- Adds only projected net interest to the goal-date total.
- Keeps interest excluded from the operating-surplus average without subtracting kept-invested interest from salary income.

## Existing records

Existing interest records are preserved as originally recorded. New confirmed interest uses the tax-aware fields automatically.
