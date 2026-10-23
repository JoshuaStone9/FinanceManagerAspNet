# Phase 15 — Simplified monthly experience

[All phases](../README.md) · [Roadmap](../../plans/roadmap.md)

**Status: Proposed.** Nothing in this folder is evidence of implementation until its document is updated with verification results.

## Purpose

Phase 15 reduces the number of concepts exposed during routine use. It introduces one primary Month destination, one month-setup workflow and an exception-led dashboard while preserving all existing calculations and financial records.

## Documents

- [15.0 — Information architecture foundation](phase-15.0-information-architecture-foundation.md)
- [15.1 — Unified Month workspace](phase-15.1-unified-month-workspace.md)
- [15.2 — Unified month setup](phase-15.2-unified-month-setup.md)
- [15.3 — Legacy Manage Month retirement](phase-15.3-legacy-manage-month-retirement.md)
- [15.4 — Exception-led dashboard](phase-15.4-exception-led-dashboard.md)
- [15.5 — Navigation and language rollout](phase-15.5-navigation-and-language-rollout.md)

## Programme constraints

- Do not change financial calculations merely to support a new layout.
- Do not delete or rewrite stored financial data during route consolidation.
- Keep old routes working until replacements are verified; add redirects before removing an interface.
- Give each financial record one primary editing path.
- Preserve the selected month and the user's position after writes.
- Keep all write actions authenticated and protected by antiforgery validation.

## Completion outcome

The sidebar exposes one Month destination rather than six category destinations. Month preparation happens once, the legacy Manage Month screen is gone, and the dashboard answers what is happening and what needs attention without repeating full workspaces.
