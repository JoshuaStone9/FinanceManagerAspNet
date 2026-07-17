# Phase 8.4.1–8.4.4 — Monthly Money Workspaces

## Status

Implemented as one consolidated batch.

## Purpose

The dashboard remains a read-only command centre. Essential Bills, Everyday Spending, Extra Expenses and Investments now have focused monthly workspaces for detailed management.

## Shared workspace architecture

The four pages use `MonthlyMoneyController`, `MonthlyMoneyWorkspaceViewModel` and one shared `Views/MonthlyMoney/Workspace.cshtml` template. Configuration supplied by each controller action changes the wording, fields, carry-over behaviour and icon without duplicating business or form logic.

## Phase 8.4.1 — Essential Bills

- Monthly total, count and average
- Existing bill picker
- Bill name, amount, date, type, duration and description
- Edit and delete actions
- Carry-over access

## Phase 8.4.2 — Everyday Spending

- Monthly total, count and average
- Existing entry picker
- Category, type and duration support
- Separate workspace for food, fuel and normal allowances
- Carry-over access

## Phase 8.4.3 — Extra Expenses

- One-off expense workspace
- Category and type support
- Explicitly excluded from carry-over
- Clear empty state when no unexpected costs exist

## Phase 8.4.4 — Investments

- Monthly contribution workspace separate from asset valuations
- Existing investment picker
- Category, duration and provider/notes support
- Carry-over access for recurring contributions

## Navigation and month preservation

Dashboard cards and sidebar entries route to the dedicated workspaces. Year and month are preserved between the dashboard, workspace tabs, previous/next month navigation and write actions.

## Data and calculation safeguards

- Existing repository methods and database tables remain the source of truth.
- No finance calculations are duplicated in Razor.
- No fixed monthly savings target is introduced.
- Assets and monthly investment contributions remain separate concepts.
- Extra expenses remain excluded from Carry over all.

## Phase 8.4.4.1 — Inline amount editing

Monthly workspace amounts can be updated directly from each recorded-entry row.

- Select the displayed amount to open a compact inline editor.
- The current amount is selected automatically.
- Press Enter to save.
- Press Escape or select Cancel to close without saving.
- Only the selected monthly record is updated.
- Full Edit remains available for the name, date, category, type, duration and notes.
- After saving, the workspace returns to the same month and entry.
- Totals and averages are recalculated from the updated database value.
- The feature is available across Essential Bills, Everyday Spending, Extra Expenses and Investments.
- Logged-out users remain read-only.

The update action reloads the existing payment before saving and preserves every field except `Amount`.

## Deferred work

Phase 8.4.5 will implement the Money Pots Existing Pot / New Pot allocation picker. Shared component extraction and the complete keyboard-first workflow remain Phase 8.4.6–8.4.7.

## Phase 8.4.4.2 — Intelligent default entry dates

New entries in Essential Bills, Everyday Spending, Extra Expenses and
Investments default to today's day number within the month currently being
managed. For example, if today is 17 July, the July workspace defaults to
17 July and the August workspace defaults to 17 August.

- The date remains editable before the entry is saved.
- The default always stays within the selected workspace month.
- For shorter months, the date is capped at that month's final valid day.
- Existing and edited records retain their stored dates.
- The legacy combined Manage Month screen follows the same rule.
