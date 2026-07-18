# Phase 8.5.1 — Income Workspace Consistency

The Monthly Income workspace now shares the existing Monthly Money recorded-entry presentation.

- Income rows use the same grid, spacing, amount alignment and actions.
- Recurring income uses the established two-state `One-off` / `Repeats monthly` switch.
- The switch posts asynchronously and only changes after the database update succeeds.
- Accessible labels, `aria-checked`, busy state and screen-reader feedback match the other workspaces.
- Editing income returns to the edited row using a URL fragment.
- No database changes are required.
