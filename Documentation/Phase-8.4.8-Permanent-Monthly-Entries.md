# Phase 8.4.8 — Recurring Monthly Entries and Month Setup

## Purpose

Recurring entries remove repetitive monthly data entry while keeping every recorded month independent and editable.

## Delivered behaviour

- Essential bills, everyday spending, investments and selected Money Pot contributions can be set to **Repeat every month**.
- Extra expenses remain one-off and cannot repeat automatically.
- New entries can be marked as recurring when they are created.
- Existing entries can start or stop repeating directly from their workspace row.
- The month setup panel lists recurring entries missing from the selected month.
- Each suggested amount comes from the latest recorded version before the month being prepared.
- Editing an amount in one month automatically makes that value the starting point for the following month.
- There is no separate default amount to maintain.
- Users can still adjust any suggested amount before adding it.
- Setup is idempotent: existing entries are never duplicated.
- Historical months are never rewritten.
- Money Pot entries are only offered when the linked pot is active and not paused for the selected month.
- Money Pot balances remain synchronized when setup creates contributions.
- New setup entries use today's day number within the selected month, capped to its final valid day.

## Example

Council Tax is £185 in July and is set to repeat monthly. August starts at £185. If August is changed to £190, September starts at £190 automatically. July remains unchanged.

## Data model

`dbo.monthly_entry_templates` now stores recurring status and the entry identity. Its stored amount remains as a fallback for the first recurrence, but subsequent month setup uses the latest recorded amount before the target month. No additional migration is required.

## Safeguards

- Unique recurring entry per source and normalized name.
- Duplicate detection before inserting into a selected month.
- Negative setup amounts are rejected.
- Future-dated records do not affect the starting value of an earlier month.
- Extra expenses are excluded.
- Inactive or paused Money Pots are excluded.

## Phase 8.4.8.2 — Recurring status clarity

Recorded-entry rows display the current recurrence state directly beneath the entry date.

- **Repeats monthly** uses an active green switch.
- **One-off** uses an inactive neutral switch.
- Selecting the switch changes the state without adding another full-sized row action.
- Edit and Delete remain the primary actions on the right-hand side.
- Confirmation messages now describe the resulting state rather than the previous action.

This makes recurrence visible at a glance and avoids the ambiguity of a button whose label only described the next action.


## Phase 8.4.8.4 — Toggle visual correction

The recurring control has exactly two visible states: **One-off** and **Repeats monthly**. Inline save feedback is visually hidden after the update so it cannot be mistaken for a third state. The switch thumb is positioned absolutely within a fixed-size track to ensure that off and on positions align correctly.
