# Phase 16.3 — Insights hub

## Status

Proposed.

## Goal

Combine reporting destinations around the questions they answer while retaining existing calculation ownership.

## Tabs

- **Snapshot** — current statistics, goals and composition summaries.
- **Trends** — month-over-month cash flow, savings and balance movement.
- **History** — finance events, audit-oriented records and export controls.

## Shared filters

- Date range or month range.
- Relevant account or pot filters where supported.
- Event type and detail level for History.

Filters should be encoded in query parameters so views remain bookmarkable and shareable within the local application.

## Acceptance criteria

- Statistics, Financial Trends and Events are no longer separate primary destinations.
- Existing totals and chart values are unchanged for equivalent inputs.
- Range labels make clear whether partial current-month data is included.
- History exports respect the visible filter set.
- Empty states link to the relevant data-entry destination rather than another report.

## Out of scope

- A new analytics data warehouse.
- Replacing source-of-truth financial calculations with calculations in Razor or JavaScript.
