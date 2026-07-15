# Phase 4.3 – Dashboard Intelligence

Implemented in this build:

- `DashboardSummaryService` as the single dashboard intelligence aggregation layer.
- Financial Action Centre with links to reminders and recommendations.
- Active pot, monthly planned, funded-this-month, and still-to-fund statistics.
- Funding health counts for healthy, overdue/missed, partially funded, paused, and completed pots.
- Recovery recommendation preview using the Stage 3.3 recommendation service.
- Due reminder preview using the Stage 3.4 reminder system.
- Upcoming incomplete targets ordered by due date and urgency.
- Automatic reminder synchronisation before the dashboard summary is calculated.
- Added a stable `#recommendations` anchor to the Household Reserve page.
- Corrected the duplicate target reminder key declaration found in `FinanceRepository`.

## Behaviour notes

- Monthly planned excludes paused and irregular pots.
- Recommendations remain advisory and do not move money automatically.
- The reminder preview shows the first three currently due open reminders.
- Upcoming targets show the next five incomplete targets.
- Target urgency is red within 30 days, amber within 60 days, and green afterwards.
