# Phase 8.3.5 — Dashboard Simplification and Navigation

## Status

Implemented.

## Purpose

The dashboard is a read-only financial command centre. It explains the selected month and routes the user to focused management workspaces rather than embedding entry forms and record tables on the dashboard itself.

## Dashboard contents

The dashboard retains:

- Financial overview
- Monthly money journey
- Essential Bills, Everyday Spending, Extra Expenses and Money Pots summaries
- Money Pots health
- 12-month forecast
- Upcoming actions
- Recent activity

The dashboard no longer contains:

- Income, carry-forward or payment forms
- Detailed monthly tables
- Delete and edit controls
- Quick-action anchors that scroll into dashboard forms
- Carry-over management controls

## Monthly management view

Detailed monthly records are now displayed in `Views/Dashboard/ManageMonth.cshtml` and selected through the existing Dashboard `Index` action using `manage=true`.

Supported sections:

- `bills`
- `everyday_spending`
- `extra_expenses`
- `investments`
- `savings`

The selected year, month and section are preserved during month navigation and after edit operations.

## Navigation

The Finance Manager sidebar now provides direct links to:

- Essential Bills
- Everyday Spending
- Extra Expenses
- Investments

Money Pots remains a dedicated Saving Pots workspace. Assets remain separate from monthly investment contributions.

## Activity language

Recent activity now displays the record name as its primary text and the action type as supporting text. For example:

- `Mortgage` / `Bill recorded`
- `Lucy’s Colouring` / `Reserve allocation recorded`

## Architecture rules

- Dashboard calculations remain in the existing dashboard services.
- No finance calculations were duplicated in Razor.
- The same `DashboardViewModel` supplies summary and management views.
- Existing write actions and validation rules remain unchanged.
- No fixed monthly savings target was introduced.

## Future work

Phase 8.4 will replace the shared monthly-management view with fully redesigned category-specific workspaces and keyboard-first entry components.
