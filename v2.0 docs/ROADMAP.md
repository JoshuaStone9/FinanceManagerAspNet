# Roadmap

## Completed
- ✅ Phase 1 – Savings Command Centre
- ✅ Phase 2 – Funding Engine
- ✅ Phase 3 – Events, Recommendations & Reminders
- ✅ Phase 4 – Dashboard Intelligence
- ✅ Phase 5 – Monthly Funding Review
- ✅ Phase 6 – Household Reserve & Pot Management Enhancements

## In progress
- 🚧 Phase 7 – Forecasting & Planning (7.1–7.6 implemented)
  - ✅ 7.1 Forecast Foundation
  - ✅ 7.2 Forecast Dashboard
  - ⏳ 7.3 Goal Risk Analysis
  - ⏳ 7.4 What-if Planner
  - ✅ 7.5 Scenario Management
  - ✅ 7.6 Dashboard Integration
  - ⏳ 7.7 Monthly Cash-flow Forecast
  - ⏳ 7.8 Forecast Recommendations

## Planned
- ⏳ Phase 8 – Reporting & Export
- ⏳ Phase 9 – Automation
- 🚧 Phase 10 – Financial Intelligence
  - ✅ 10.1 Transparent Statistics Forecasting
  - ✅ 10.2 Clean Test Data
  - ✅ 10.3 Safe Reset Controls
  - ✅ 10.4 Money Pot Activity


## Phase 11.6.3 - Reconciliation notification refinement

- Account Management uses one top-level reconciliation notification instead of repeating pending-interest details on collapsed account cards.
- The notification shows affected accounts, the pending total, and the age/date of the oldest unreconciled interest payment.
- Review Accounts opens the oldest affected account directly in its balance-check panel.
- Pending-interest details remain available only inside Update Balance, keeping the main account grid focused.

## Phase 11.9 — Event Exports and Emergency Fund Restoration

- Added filtered CSV and PDF exports to Finance Events.
- Added a dedicated Emergency Fund contribution workflow.
- Emergency Fund top-ups are recorded as allocations rather than Extra Expenses.
- Other pots remain fundable while the Emergency Fund is below its base level, with an advisory shortfall message.
- Added structured `EmergencyFundContribution` audit events.
