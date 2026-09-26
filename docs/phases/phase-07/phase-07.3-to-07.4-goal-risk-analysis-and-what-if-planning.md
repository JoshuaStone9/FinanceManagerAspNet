# Phase 7.3–7.4 – Goal Risk Analysis and What-if Planning

Status: ✅ Implemented

## Phase 7.3 Goal Risk Analysis

Goal forecasts now include:

- Forecast status separate from monthly funding and live pot status
- Required monthly contribution
- Additional monthly contribution required
- Months early or late against the target date
- Detailed explanation of the forecast result
- Plain-English recommended action

The status order intentionally prioritises Overdrawn, then missing target information, completed goals and finally timing risk.

## Phase 7.4 What-if Planner

`ForecastController.WhatIf` and `Views/Forecast/WhatIf.cshtml` allow temporary testing of:

- Forecast timeframe
- Selected pot
- Monthly contribution
- Target amount
- Target date
- One-off contribution
- Account interest rate
- Protected reserve baseline
- Future expense amount and date

`IWhatIfForecastService` clones the required account and pot values with C# record `with` expressions. The live objects are never mutated and no repository write method is called.

## Test coverage

Batch 2 adds tests for months late, recommendations, one-off contributions, future expenses, live-object immutability and temporary what-if overrides.

## Database impact

None. What-if scenarios are request-scoped and are not stored. Persistent saved scenarios are reserved for Phase 7.5.

## Acceptance criteria

- Risk explanations clearly state why a goal has its status.
- Additional required contribution is shown where calculable.
- Current and scenario forecasts can be compared side by side.
- What-if changes do not alter balances, pot settings, reserve accounts or history.
- Future expenses affect only the selected forecast month.
