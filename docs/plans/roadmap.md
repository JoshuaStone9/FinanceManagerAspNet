# Planning guide

[Documentation](../README.md) · [All phases](../phases/README.md)

The former roadmap combined old phase numbering, stale statuses and later release notes. Its complete text is retained in the [historical roadmap](../history/roadmap-before-documentation-review.md). Use the phase guides for the documented development sequence.

## Active simplification programme

Phases 15–17 define the next proposed development sequence. They deliberately separate user-facing simplification from later internal restructuring:

1. [Phase 15 — Simplified monthly experience](../phases/phase-15/README.md): establish the new information architecture, create one Month destination, unify month setup, retire the legacy Manage Month screen and simplify the dashboard.
2. [Phase 16 — Consolidated financial hubs](../phases/phase-16/README.md): combine related account, pot, planning and insight destinations behind stable hub navigation.
3. [Phase 17 — Internal simplification](../phases/phase-17/README.md): split broad data-access responsibilities, remove startup schema management, simplify controllers and shared views, and strengthen verification.

The programme must preserve financial calculations and stored data. Existing routes remain compatible until their replacements are verified, and route removals require redirects.

### Programme success measures

- Six primary finance destinations replace the current controller-oriented navigation.
- Month has one setup workflow and every monthly record has one primary editing path.
- Home presents current position and exceptions without duplicating specialist workspaces.
- Legacy bookmarks redirect to the correct destination, selected month and context.
- Shared totals agree across Home, Month and Insights for the same period.
- Data access, controllers and Razor views have clear ownership boundaries.
- A clean checkout can build and run the documented test suite without creating tracked artifacts.

## Other proposals recorded in the documents

These are proposals as written, not a verified current backlog.

- [Phase 7](../phases/phase-07/README.md): the overview still lists monthly cash-flow forecasting and forecast recommendations as planned after 7.1–7.6.
- [Reporting and exports](../phases/phase-08/history/phase-08-reporting-and-export.md): an early proposal. [Phase 11.9](../phases/phase-11/phase-11.9-event-exports-and-emergency-fund-restoration.md) subsequently documents event CSV/PDF exports; other proposed export features need separate assessment.
- [Automation](../phases/phase-09/history/phase-09-automation.md): an early proposal, distinct from the later Settings phase.
- [Phase 14.3](../phases/phase-14/phase-14.3-statement-import-and-matching.md): delivers PDF extraction, import previews, duplicate protection and automatic match scoring.
- [Future ideas](future-ideas.md): a placeholder for new proposals.

Add new plans here by linking to their owning phase or topic. Keep delivered changes in phase records rather than appending unrelated release notes to this guide.
