# Phase 7 – Forecasting & Planning

Status: 🚧 In progress

## Objective

Turn Finance Manager from a system that records and reviews finances into one that can explain likely future outcomes. Forecasting is advisory and read-only: it must never silently alter balances, pot settings, historical funding records or live monthly budgets.

## Status model

Phase 7 adds a forecast status alongside the existing funding and pot statuses.

- Monthly funding status answers: what happened this month?
- Overall pot status answers: what is the pot's current live state?
- Forecast status answers: is the goal likely to be achieved?

Forecast statuses are Completed, On track, At risk, Behind, No contribution planned, No target, No due date and Overdrawn.

## 7.1 – Forecast Foundation

Implemented in Batch 1.

### Components

- `IFinancialForecastService` and `FinancialForecastService`
- `FinancialForecastRequest`
- `FinancialForecastResult`
- `MonthlyForecastRow`
- `PotForecastResult`
- `ForecastGoalStatus`
- Dedicated xUnit test project

### Inputs

- Selected household reserve accounts
- Opening balances and account interest rates
- Configured reserve-account monthly contributions
- Protected household reserve baseline
- Active reserve pots
- Current pot balances
- Intended monthly pot contributions
- Pot targets and due dates
- Funding pause periods
- User-selected forecast end date

### Calculation rules

1. Selected reserve accounts provide the opening reserve balance.
2. Each account is forecast separately so its own interest rate is respected.
3. Interest is estimated monthly.
4. Future account contributions can be included or excluded.
5. Pot contributions are virtual allocations inside the reserve and do not reduce the reserve total.
6. The protected baseline is deducted before unallocated surplus is reported.
7. Negative pot balances are classified as Overdrawn before any other forecast status.
8. A target completed on or before its due date is On track.
9. Completion within two months after the due date is At risk.
10. Later completion, an expired due date or an unreachable goal is Behind.
11. Forecast operations do not write to the database.

### Automated tests

Batch 1 covers baseline protection, account contributions, contribution exclusion, monthly interest, completed goals, overdrawn pots, missing targets, missing due dates, zero contributions, on-track goals, at-risk goals and behind goals.

## 7.2 – Forecast Dashboard

Implemented in Batch 1.

### Page

`ForecastController.Index` and `Views/Forecast/Index.cshtml` provide:

- 3, 6, 12 and 24-month timeframes
- Custom end date
- Optional future account contributions
- Current and projected reserve totals
- Forecast interest and contributions
- Projected pot allocations
- Protected and unallocated surplus
- Goals completed and goals needing attention
- Month-by-month reserve projection
- Pot completion forecasts
- Required contribution comparisons
- Visible forecast assumptions

The main Finance Manager navigation now includes Forecast.

## 7.3 – Goal Risk Analysis

Implemented in Batch 2.

Each active pot now receives a detailed risk explanation, an exact additional monthly contribution where calculable, a months-early-or-late result and a plain-English recommended action. Forecast status remains separate from monthly funding status and overall pot status.

## 7.4 – What-if Planner

Implemented in Batch 2.

The dedicated What-if Planner supports temporary overrides for a selected pot contribution, target, target date and one-off contribution, plus account interest rate, protected baseline, future expense and forecast timeframe. It compares the current and scenario plans without writing to live settings or history.

## 7.5 – Scenario Management

Implemented in Batch 3.

Planning scenarios can be saved, renamed, duplicated, deleted, compared and marked as preferred. Preferred scenarios remain isolated from live balances, pot settings and contribution history.

## 7.6 – Dashboard Integration

Implemented in Batch 3.

The main dashboard now includes a compact 12-month forecast summary showing projected reserve, projected interest, goals at risk and goals expected to complete. It uses the preferred saved scenario when one exists, otherwise it uses the live plan.

## 7.7 – Monthly Cash-flow Forecast

Planned for Batch 4.

Forecast future income, bills, everyday spending, investments, pot allocations, planned expenses, interest and remaining money. Clearly distinguish historical actuals, the current month and future forecast months.

## 7.8 – Forecast Recommendations

Planned for Batch 4.

Provide advisory recommendations such as increasing a contribution, making a one-off payment, delaying a lower-priority goal, restoring an overdrawn pot or reallocating surplus. Recommendations require explicit confirmation before any future apply workflow changes live data.

## Phase 7 completion criteria

Phase 7 is complete when future reserve balances and interest can be forecast, each targeted pot receives a completion outlook, goal risk is explained, what-if changes are isolated from live data, scenarios can be saved and compared, monthly cash flow can be projected, the dashboard provides a compact outlook, assumptions are visible and core calculations are covered by automated tests.
