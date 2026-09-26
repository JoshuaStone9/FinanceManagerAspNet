# Phase 8.4.6 — Money Pots Monthly Funding Workspace

A dedicated Monthly Money workspace now restores the monthly funding workflow that previously lived on the combined dashboard management screen.

## Included

- A fifth Monthly Money workspace named **Money Pots**.
- Active-pot selection rather than free-text pot creation.
- Contribution amount, editable date and optional notes.
- The date defaults to today’s day within the selected month.
- Monthly contribution history with inline amount editing, full edit and delete actions.
- Adding, editing and deleting a contribution keeps the corresponding pot balance synchronized.
- Navigation returns to the selected month and workspace.
- The main Money Pots page remains responsible for creating pots, targets and advanced funding settings.

No database migration is required. Contributions continue to use the existing `savings` records and reserve-pot allocation logic.
