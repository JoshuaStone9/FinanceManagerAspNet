# Changelog

## Phase 10.4 — Money Pot Activity
- Removed the legacy Monthly Funding Review page and its rigid planned/recovery/excess terminology.
- Added a contribution-driven Money Pot Activity page with monthly summaries, a contribution timeline, goal progress and forecast insights.
- Updated the Finance navigation to use Pot Activity.

## Phase 8.4.1 Batch 1 — Allocation pot-link data foundation

- Added nullable reserve-pot links and pot-name snapshots to dashboard reserve allocations.
- Added safe, idempotent backfill for unique exact historical name matches.
- Added foreign-key and index protection without deleting unmatched history.
- Extended payment reads with linked-pot metadata and display-name fallback.
- Added repository queries needed by the upcoming allocation picker service and UI.

## Phase 7.5–7.6 – Scenario Management and Dashboard Integration

- Added persistent saved forecast scenarios.
- Added rename, duplicate, delete, preferred-scenario and comparison workflows.
- Kept preferred scenarios isolated from live finance data.
- Added a compact 12-month forecast summary to the main dashboard.
- Added automatic database bootstrap for `forecast_scenarios`.


## Phase 7.1–7.2 – Forecast Foundation and Dashboard

- Added a read-only financial forecast engine.
- Added month-by-month reserve and interest projections.
- Added pot completion, required contribution and forecast status calculations.
- Added the Forecast page and navigation entry.
- Added visible assumptions and timeframe controls.
- Added the FinanceManagerAspNet.Tests xUnit project and initial forecast tests.
# Changelog

## Version 2.5
- Dashboard Intelligence
- Recommendation workflow
- Monthly Funding Review

## Next
- Forecasting & Planning


## Phase 7 Batch 2
- Added detailed goal risk analysis and recommendations.
- Added a read-only what-if planner with temporary overrides and comparison.
- Added unit tests for risk and scenario behaviour.

## Phase 8 Batch 1 — 8.1 to 8.3
- Replaced the Finance Manager topbar with a responsive, collapsible sidebar shell.
- Grouped navigation into Dashboard, Finance, Money and Personal sections.
- Added active-page highlighting, mobile drawer behaviour and persisted collapse state.
- Added the Finance Manager logo and favicon to the application shell.
- Refreshed the dashboard header and content hierarchy while preserving existing data and actions.
- Added Phase 8 roadmap, design-system, navigation, component and sidebar documentation.

## Phase 10.7 — Passive Income Engine
- Added automatic monthly interest estimates for interest-bearing accounts.
- Added expected-versus-received passive income to the Income workspace.
- Confirmed interest is recorded as real monthly income only after receipt.
- Added duplicate protection, backup/restore support and safe monthly reset support.
