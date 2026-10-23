# Phase 15.3 — Legacy Manage Month retirement

## Status

Proposed.

## Goal

Remove the parallel `Dashboard?manage=true` editing experience after all of its supported tasks have verified replacements in Month.

## Migration work

- Move starting-adjustment creation, editing and removal to Month Overview.
- Route income management through the Month income workspace.
- Route payment creation, editing and deletion through the owning Month tab.
- Move remaining-surplus allocation to the relevant Pot funding or Month completion flow.
- Redirect legacy Manage Month URLs to the equivalent Month tab and selected month.
- Preserve useful fragment or focus behaviour when redirecting.

## Removal work

- Remove `Views/Dashboard/ManageMonth.cshtml`.
- Remove the `manage` and legacy `source` presentation branches from the Dashboard index action.
- Remove duplicated Dashboard write actions only after all forms use their canonical replacements.
- Remove CSS and JavaScript used only by the retired interface.

## Acceptance criteria

- Every control previously available on Manage Month has a documented replacement or an explicit retirement decision.
- Old bookmarked URLs reach the correct selected month.
- Search of views and scripts finds no links to `manage=true`.
- DashboardController no longer owns generic monthly payment CRUD.
- Regression tests confirm totals and stored records are unchanged by the route migration.

## Rollback boundary

Redirects should be introduced before view and action deletion. The deletion commit must remain independently revertible without requiring a database rollback.
