# Stage 3.3 and 3.4 implementation

## Stage 3.3 – recommendation service

- Added `IReserveRecommendationService` and `ReserveRecommendationService`.
- Recovery recommendations are no longer calculated inside the Razor view.
- Available unallocated reserve money is distributed by active-pot priority.
- Each recommendation shows the suggested amount, total recovery outstanding, and any balance that would remain.
- Recommendations remain advisory and do not move money automatically.

## Stage 3.4 – reminders

- Added the `finance_reminders` table through the existing startup schema upgrade.
- Added automatic reminders for overdue, missed, and partially funded pots.
- Added automatic reminders for incomplete pot targets due within 30 days.
- Added a Reminders page and navigation entry.
- Added manual reminder creation with an optional Household Reserve pot link.
- Added Open, Completed, Dismissed, and All filters.
- Added complete, dismiss, reopen, and snooze actions.
- Added due-reminder summaries to the Household Reserve page.
- Reminder actions require login; reminder viewing remains available in read-only mode.
