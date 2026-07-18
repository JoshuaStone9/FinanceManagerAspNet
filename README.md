# Finance Manager V2

Finance Manager V2 is a modern ASP.NET Core MVC personal finance and financial planning application designed to provide complete visibility and control over household finances, savings goals, reserve funds and long-term financial planning.

The application has evolved from the original WinForms Finance Manager into a service-oriented web application with dashboard intelligence, funding automation and historical reporting while remaining compatible with the existing SQL Server data model.

---

# Current Version

**Version:** 2.0

**Status:** Active Development

**Current Development Phase:** Phase 6 – Forecasting & Planning (Planned)

---

# Technology Stack

* ASP.NET Core MVC
* C#
* SQL Server (LocalDB during development)
* Razor Views
* Bootstrap
* Service / Repository Architecture

---

# Core Principles

Finance Manager V2 has been designed around several core principles:

* Preserve historical financial information.
* Never automatically move money without user confirmation.
* Keep funding history immutable.
* Preserve the user's location after save operations.
* Keep business logic within services.
* Use dashboard intelligence to highlight actionable information.
* Separate operational dashboards from historical reporting.

---

# Major Features

## Dashboard

* Financial Action Centre
* Funding Health overview
* Due reminders
* Recommendation summary
* Upcoming targets
* Funding statistics

## Money Pots

* Reserve balance management
* Multiple savings pots
* Individual targets
* Pot priorities
* Active and paused pots
* Carry excess configuration
* Funding plans

## Savings Command Centre

* Monthly funding plans
* Funding frequencies
* Funding dates
* Carry excess
* Carry shortfalls
* Pause periods
* Target dates
* Contribution history

## Funding Engine

* Monthly funding tracking
* Recovery calculations
* Genuine excess
* Carried excess
* Funding timeline
* Funding history
* Automatic monthly summaries

## Recommendation Engine

* Recovery recommendations
* Priority-based allocation
* Apply individual recommendations
* Apply all recommendations
* Recommendation history

## Finance Events

Complete financial audit trail including:

* Pot creation
* Pot updates
* Contributions
* Funding status changes
* Recommendation applications
* Funding rebuilds

## Reminders

* Manual reminders
* Automatic reminders
* Funding reminders
* Target reminders
* Snooze
* Complete
* Dismiss
* Reopen

## Monthly Funding Review

* Month selector
* Summary cards
* Per-pot funding review
* Monthly funding statistics
* Events
* Reminders
* Recommendation history

## Statistics

* Reserve statistics
* Goal tracking
* Interest calculations
* Historical summaries

---

# Database

Finance Manager V2 remains compatible with the original Finance Manager database while extending it with additional tables where required.

Original tables remain supported alongside new tables for:

* Funding history
* Finance events
* Finance reminders
* Recommendation applications
* Dashboard intelligence
* Monthly reviews

---

# Configuration

Configuration is stored within `appsettings.json`.

Example:

```json
{
  "ConnectionStrings": {
    "FinanceManager": "Server=(localdb)\\MSSQLLocalDB2025;Database=Finance_Manager_V2;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```

---

# Architecture

Finance Manager follows a layered architecture.

```text
Razor Views
      │
Controllers
      │
Services
      │
FinanceRepository
      │
SQL Server
```

Business logic is implemented within services while repositories are responsible only for data access.

---

# Running the Project

1. Open the solution in Visual Studio 2022 or later.
2. Restore NuGet packages.
3. Configure `appsettings.json`.
4. Apply any pending database migrations or allow automatic table creation.
5. Run the application.

---

# Documentation

Project documentation is located within the `/docs` folder.

* Specification
* Roadmap
* Architecture
* Database
* Coding Standards
* UI Standards
* Phase Documentation
* Changelog

---

# Roadmap

## Completed

* Phase 1 – Savings Command Centre
* Phase 2 – Funding Engine
* Phase 3 – Events, Recommendations & Reminders
* Phase 4 – Dashboard Intelligence & Recommendation Workflow
* Phase 5 – Monthly Funding Review

## Planned

* Phase 6 – Forecasting & Planning
* Phase 7 – Reporting & Export
* Phase 8 – Automation
* Phase 9 – Financial Intelligence

---

# Development Standards

The project follows several development standards:

* Service-oriented architecture
* Repository pattern
* Async-first data access
* Immutable funding history
* Interface-based collection parameters (`IReadOnlyList<T>` / `IEnumerable<T>`) where appropriate
* Confirmation before financial actions
* Version-controlled project documentation

---

Finance Manager V2 continues to evolve into a complete personal financial planning platform, providing budgeting, reserve management, funding automation, historical reporting and long-term financial forecasting from a single application.

## Phase 8.3 dashboard experience
The dashboard now includes a monthly money journey, category summaries, planning outlook, upcoming actions, recent activity, quick actions and responsive command-centre styling. See `Documentation/Phase-8.3-Dashboard-Experience.md`.

## Phase 8.3.7 — Razor rendering corrections

Corrected dashboard Razor expressions that were being emitted as literal text rather than evaluated values. Arithmetic and negative currency expressions are now enclosed as complete Razor expressions before formatting. The remembered-payment picker was also restructured so its conditional searchable input/select markup is parsed as Razor rather than displayed on screen.

## Phase 8.3.5 — Dashboard simplification and navigation

The Finance Manager dashboard is now a read-only command centre. Detailed monthly forms, tables and editing controls have moved to a separate monthly-management view. Dashboard summary cards preserve the selected year and month when opening the relevant management area, while Money Pots and Forecast continue to open their existing dedicated workspaces.

The sidebar now includes direct monthly-money navigation for Essential Bills, Everyday Spending, Extra Expenses and Investments. Recent activity uses natural record-first wording, and the dashboard ends after Planning and Attention/Activity.

## Phase 8.4.1–8.4.4 — Monthly money workspaces

Essential Bills, Everyday Spending, Extra Expenses and Investments now open as dedicated monthly workspaces rather than filtered sections of the dashboard management page. The workspaces share a consistent responsive layout, month navigation, existing-entry prefilling, entry history, edit/delete actions and category-specific fields. Dashboard links and sidebar navigation preserve the selected month. Extra expenses remain explicitly excluded from carry-over, while investments remain separate from asset holdings and valuations.


## Phase 8.4.4.1 — Inline amount editing

Recorded monthly entries now support click-to-edit amounts in Essential Bills, Everyday Spending, Extra Expenses and Investments. Enter saves, Escape cancels, and the update changes only the selected monthly record before returning to the same row.

## Phase 8.4.4.2 — Default entry date

New monthly bills, everyday spending entries, extra expenses and investment
contributions now default to today's day number within the selected month.
The date remains editable and is capped to the final valid day for shorter
months.


## Phase 8.4.5 — Money Pots

The former Household Reserve workspace is now presented as **Money Pots** throughout the user interface. The refreshed page uses clearer banking-style language, a stronger savings summary, active-pot and target information, cleaner collapsed pot cards, improved empty-state guidance and more approachable actions such as **Create money pot**. Internal model, repository and database names remain unchanged to avoid unnecessary migrations and code churn.


## Phase 8.4.6 — Money Pots monthly funding

Monthly Money now includes a dedicated Money Pots workspace for recording contributions to active pots. Contributions update existing pot balances and can be added, edited, deleted or adjusted inline without returning the management form to the dashboard.

## Phase 8.4.7 — Keyboard-first monthly workflow

The five monthly money workspaces now support consistent keyboard entry: Enter saves, Escape clears, successful saves return focus to the first input, and validation failures preserve the current draft. Previous-entry selection and inline amount editing remain optimized for rapid monthly updates.

## Phase 8.4.8 — Recurring monthly entries and month setup

Bills, everyday spending, investments and selected Money Pot contributions can now repeat each month. Month setup uses the latest recorded amount before the month being prepared, so changing an amount once becomes the starting value for the following month without maintaining a separate default. Historical months remain unchanged, duplicates are prevented, extra expenses stay one-off, and inactive or paused Money Pots are excluded.


## Phase 8.4.8.2 — Clear recurring status

Monthly workspace rows now show a compact **Repeats monthly** or **One-off** switch beneath the entry date. The switch communicates the current state directly and replaces the previous large repeat/stop-repeating action button, leaving Edit and Delete as the primary row actions.


## Phase 8.4.8.3 — Functional recurring switches

Recurring status switches now update in place without a full-page reload. The control is disabled while saving, displays live feedback, and immediately changes between `Repeats monthly` and `One-off` after the server confirms the update.


## Phase 8.4.8.4 — Two-state recurring toggle

Recurring controls now display only two visual states: **One-off** and **Repeats monthly**. The switch track and thumb use fixed positioning for consistent alignment, while AJAX confirmation remains available to screen readers without leaving a third-looking status message beneath the entry.

## Phase 8.4.8.5 — Prepare next month

The former editable Carry over review has been replaced by a compact Prepare next month workflow. It adds only missing recurring entries using their latest recorded amounts, never replaces existing destination-month entries, excludes one-off entries and extra expenses, and carries the completed month's excess or shortfall separately as a financial balance.

## Phase 8.4.9 — Shared Components, Accessibility & UI Polish

The five Monthly Money workspaces now share extracted tab and metric partials, clearer keyboard focus, entry-specific accessible action labels, improved long-name and mobile handling, reduced-motion support, and a Razor-safe Prepare Next Month view. This phase changes presentation and maintainability only; no database migration is required.

### Phase 8.4.9.1 — Recurring month-setup filtering

Month setup now offers only active recurring entries that existed in the immediately preceding month for the same workspace. Old, deleted, or unrelated recurring definitions no longer reappear as setup suggestions.

## Phase 9.1 — Monthly Financial Health Summary

The dashboard now includes a financial health section that interprets the selected month's income, spending and future allocations. It reports income committed, essential-bill pressure, monthly surplus or shortfall, comparisons with the previous month and plain-English insights. No database migration is required.


## Phase 8.5 — Monthly Income Workspace

Income is now the first Monthly Money workspace. Each month can contain multiple actual income sources, including salaries, overtime, bonuses, benefits and other income. Sources can repeat monthly or remain one-off, and the dashboard financial-health calculations now use the summed monthly entries instead of relying on a fixed configured income whenever actual entries exist.

### Phase 8.5.1 — Income workspace consistency

Income records now use the same recorded-entry row layout and two-state recurring control as the other Monthly Money workspaces. The income recurring switch saves asynchronously, exposes the same accessible state, and uses the shared spacing and action alignment. Income edits also return to the edited row.


## Phase 8.6 — Money Pots & Monthly Entry Simplification
Monthly entry forms now use workspace-specific types and support either month-count or end-date durations. Money Pot creation and editing have been reduced to name, optional target, optional date needed by and notes. Forecasts now use real contribution history, while explicit Pause/Resume actions replace legacy priority and carry-forward controls.


## Phase 8.6.1 — Forecast consistency

The Forecast page now uses the same contribution-driven Money Pot calculations as the main Money Pots page. Recent real contribution history replaces legacy intended-contribution fields in forecast cards, so status, estimated completion and timing remain consistent across the application.


## Phase 9.1.1 — Financial health presentation fix

The dashboard financial-health summary now uses an isolated, versioned stylesheet so its score card, progress meter, status pill and insight cards render consistently. This corrects the unstyled stacked presentation that could occur after the Phase 8.6 style consolidation.

## Phase 9 — Settings & Application Configuration

Settings is now a full application control centre covering finance configuration, the protected emergency-fund baseline, appearance, security, portable JSON backups, restore, CSV export, finance-data reset and system diagnostics. The £12,000 baseline is visible and editable under Settings → Finance and is used by the reserve summary shared across Dashboard, Money Pots and Forecast.
