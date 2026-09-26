# Phase 8.3 — Dashboard Experience

## Status
Implemented as one consolidated batch covering 8.3.2 through 8.3.6.

## Objective
Turn the dashboard into the Finance Manager command centre. The dashboard answers where the month's money went, what remains, what is allocated to the future, and what requires attention without replacing the detailed editing tools.

## Delivered
- Shared dashboard metric, summary, planning, list, activity, empty-state and quick-action patterns.
- A monthly money journey from income and carry forward through spending, investments and Money Pots allocations to the final remaining amount.
- Dedicated summaries for Essential Bills, Everyday Spending, Extra Expenses and Money Pots.
- Existing Phase 7.6 forecast data surfaced in the planning section.
- Upcoming actions built from real reminders, overdue funding state and upcoming pot target dates.
- Recent activity built from the selected month's real payment/allocation records.
- Responsive desktop, tablet and mobile behaviour.
- Visible focus behaviour inherited from the design system and reduced-motion support.
- Existing forms retained under “Manage this month”.

## Calculation rules
The dashboard does not introduce a fixed monthly savings target.

1. Effective income = monthly income + carry forward.
2. Core spending = essential bills + everyday spending + extra expenses.
3. Available before future allocations = effective income - core spending.
4. Allocated to the future = investments + Money Pots allocations.
5. Final remaining = effective income - all recorded monthly allocations.

Extra expenses remain separate and are not included in carry-over. Money Pots allocations remain distinct from investments. All figures use the existing selected-month data loaded by DashboardController.

## Architecture
`DashboardExperienceService` converts the existing DashboardViewModel data into presentation-focused models. Razor displays these models and contains no alternative financial formula.

Models:
- DashboardExperience
- MonthlyMoneyJourney
- DashboardSectionSummary
- DashboardActionItem
- DashboardActivityItem

The service is registered through `IDashboardExperienceService` and called after the existing intelligence and forecast summaries are complete.

## Empty states
Sections present explicit empty states when there are no upcoming actions or recent records. No placeholder financial values are invented.

## Responsive rules
- Four-column metrics reduce to two columns and then one.
- Planning and activity panels reduce from two columns to one.
- The money journey becomes vertical on narrow screens.
- Quick actions reduce from five columns to three and then one.

## Deferred work
Phase 8.4 form modernisation remains separate. The Money Pots allocation picker data foundation remains present, but the existing dashboard allocation form is intentionally unchanged until 8.4 resumes.
