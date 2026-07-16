# Phase 6.5 – Negative Pot Balances and Recovery

Status: Implemented

## Included
- Clear overdrawn-pot status and recovery amount.
- Partial or full one-off recovery contributions.
- Normal intended monthly contributions remain unchanged.
- Negative pots are prioritised by the recommendation engine.
- Automatic negative-balance reminders.
- Automatic reminder completion when restored.
- Finance Events for partial and full restoration.
- Duplicate-submission protection.

## Events
- `PotOverdrawn`
- `NegativeBalancePartiallyRestored`
- `NegativeBalanceRestored`

## Rules
- Negative allocations never count as freely allocatable reserve money.
- Recovery contributions are capped at the amount required to return the pot to £0.
- Extra recovery contributions do not replace or alter the configured monthly contribution.
