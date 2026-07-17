# Phase 8 — UI and Experience Modernisation

## Current status
- 8.1 Application shell — complete
- 8.2 Navigation redesign — complete
- 8.3 Dashboard experience — complete
- 8.4 Unified forms and components — data foundation started; UI work paused until after 8.3
- 8.5 Responsive experience — dashboard coverage complete; application-wide pass pending
- 8.6 Accessibility — dashboard coverage complete; application-wide audit pending
- 8.7 Branding and identity — initial logo and favicon complete
- 8.8 Settings centre — pending
- 8.9 Micro-interactions — pending
- 8.10 Performance optimisation — pending
- 8.11 Final UI polish — pending

## Phase 8.3 delivered scope
8.3.2 shared dashboard cards and layout, 8.3.3 monthly money journey, 8.3.4 category summaries, 8.3.5 upcoming actions and recent activity, and 8.3.6 responsive/accessibility polish were implemented together in one batch.


### 8.4.6 Money Pots Monthly Funding Workspace — Complete

Restores monthly pot contribution entry as a dedicated workspace with active-pot selection and synchronized balances.

### 8.4.7 Keyboard-First Monthly Workflow — Complete

All five monthly workspaces now share a faster keyboard workflow. Enter submits the current quick entry, Escape clears it, previous-entry selection moves directly to the amount, and successful saves return focus to the first field ready for another entry. Invalid submissions preserve the entered draft and focus the field that needs attention. Inline amount editing continues to support Enter to save and Escape to cancel.

### 8.4.8 Recurring Monthly Entries and Month Setup — Complete

Recurring entry status, selectable month setup, latest-recorded-amount carry-forward, duplicate prevention and active/paused Money Pot safeguards are now implemented across the monthly workspaces. Extra expenses remain one-off by design. Recurring state is now displayed using a compact, explicit Repeats monthly / One-off switch within each row.

### 8.4.8.5 — Prepare next month — Complete

Replaced the duplicate editable Carry over review with a compact, non-destructive recurring-entry preparation flow. Carry-forward balances remain separate from recurring records.
