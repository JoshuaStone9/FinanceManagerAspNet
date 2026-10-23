# Phase 17.3 — Shared view components

## Status

Proposed.

## Goal

Reduce large Razor files and inconsistent interaction patterns by extracting repeated presentation components without moving calculations into views.

## Candidate components

- Month selector.
- Hub tabs and workspace tabs.
- Financial metric cards.
- Status badges and warning summaries.
- Empty states.
- Entry tables and row actions.
- Confirmation and destructive-action panels.
- Account, pot and scenario selectors.

## Implementation rules

- Reuse or extend the design system before introducing new component-specific styling.
- Prefer partials for passive markup and view components when server-side composition is required.
- Pass typed models rather than relying on broad ViewBag state.
- Keep JavaScript behaviour progressively enhanced and keyboard accessible.
- Delete superseded CSS only after all consumers have migrated.

## Acceptance criteria

- The largest finance Razor views are divided into cohesive, typed units.
- Repeated markup has one canonical implementation where behaviour is genuinely shared.
- Dark/light themes and all supported breakpoints remain functional.
- Component extraction does not change financial totals or form destinations.
- Accessibility checks cover labels, focus, validation and status announcements.
