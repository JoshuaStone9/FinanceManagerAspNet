# Phase 8.4.9.1 — Recurring Month-Setup Filtering

The monthly setup panel is now scoped to valid recurring entries from the immediately preceding calendar month.

An entry is offered only when it:

- belongs to the current monthly workspace;
- has an active recurring definition;
- existed in the immediately preceding month;
- is not already present in the selected month; and
- remains eligible, including active and unpaused Money Pots.

This prevents stale definitions, deleted entries, and entries from unrelated workspaces from resurfacing unexpectedly.
