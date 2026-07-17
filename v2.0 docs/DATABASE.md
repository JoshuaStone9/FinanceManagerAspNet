# Database

Document each table with:
- Purpose
- Primary Key
- Foreign Keys
- Relationships
- Business Rules

Current key tables include:
- reserve_pots
- reserve_pot_monthly_funding
- finance_events
- finance_reminders
- recommendation_applications
- household_reserve


## Phase 7 Batch 1

Phase 7.1–7.2 introduces no database tables or migrations. Forecasts are calculated in memory from existing selected reserve accounts and reserve pots. Saved scenario persistence is deferred to Phase 7.5.


## Phase 7 Batch 3

Adds `forecast_scenarios` for persistent what-if planning. The table stores scenario names, preferred status, forecast duration and all supported Phase 7.4 overrides. Saved scenarios never overwrite live reserve accounts, pots, balances or funding history.
