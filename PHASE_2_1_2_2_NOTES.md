# Phase 2.1 and 2.2

Implemented as a dedicated `IFundingEngineService` so status and allocation rules have one source of truth.

## Phase 2.1 statuses

- Inactive
- Not configured
- Pending
- In progress
- Overdue
- Partially funded
- Missed
- Funded
- Overfunded
- Funded from carried excess
- Paused
- Paused – voluntary contribution

## Phase 2.2 allocation order

1. Carried credit and the current dashboard contribution satisfy the current month.
2. Remaining dashboard money clears the oldest accumulated recovery balance.
3. Remaining money becomes carried future credit when enabled.
4. Otherwise it is recorded as genuine excess.

The repository now supplies monthly inputs and persists the pure service result. No calculation rules remain duplicated in the repository loop.
