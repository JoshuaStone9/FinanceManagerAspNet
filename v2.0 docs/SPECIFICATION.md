# Finance Manager V2 Technical Design & Product Specification

## Vision
Provide a single application to manage budgeting, household reserves, savings goals, funding plans, reminders, financial events and long-term planning.

## Core Principles
- Preserve user position after save.
- Funding history is immutable unless explicitly rebuilt.
- Recommendations never move money automatically.
- Dashboard shows operational status.
- Reports show historical information.
- Forecasts never modify live financial data.

## Implemented Modules
### Dashboard
Operational summary and action centre.

### Household Reserve
Reserve balance management and savings pots.

### Savings Command Centre
Funding configuration, priorities, carry excess, pause periods.

### Funding Engine
Monthly funding, recovery, genuine excess, carried excess and funding history.

### Events
Complete audit trail.

### Reminders
Manual and automatic reminders.

### Recommendation Engine
Recovery recommendations and application workflow.

### Monthly Funding Review
Monthly summaries, funding outcomes, events and reminders.

### Forecasting & Planning
A read-only forecast engine and dedicated dashboard project selected reserve-account balances, account contributions, monthly interest, protected reserve surplus and pot completion outcomes. Forecast status is separate from monthly funding status and overall pot status. Later Phase 7 batches add detailed goal risk analysis, what-if planning, saved scenarios, cash-flow forecasting, dashboard integration and advisory forecast recommendations.

## Forecasting Business Rules
- Forecasts begin from current selected reserve-account and active pot balances.
- Account interest is calculated separately using each account's rate.
- The protected household reserve baseline remains unavailable for allocation.
- Pot contributions are virtual reserve allocations and do not reduce the reserve total.
- Forecast statuses are Completed, On track, At risk, Behind, No contribution planned, No target, No due date and Overdrawn.
- Forecast calculations never write to live financial tables.
- What-if and saved scenario values remain isolated until a separate confirmed workflow applies a change.
- Assumptions must be displayed beside forecast results.

## Future Modules
See ROADMAP.md.
