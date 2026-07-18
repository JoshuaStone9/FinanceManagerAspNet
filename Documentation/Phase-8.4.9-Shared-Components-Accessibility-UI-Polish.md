# Phase 8.4.9 — Shared Components, Accessibility & UI Polish

This phase consolidates the monthly workspace interface and improves usability without changing financial behaviour.

## Shared components

- Extracted the monthly workspace tab navigation into `_WorkspaceTabs.cshtml`.
- Extracted the four workspace summary cards into `_WorkspaceMetrics.cshtml`.
- All five monthly workspaces continue to use the same `Workspace.cshtml` screen and therefore receive the same behaviour and styling.

## Accessibility

- Active workspace tabs now expose `aria-current="page"`.
- Edit and delete controls include entry-specific accessible names.
- Recurring switches identify the entry they affect.
- Focus-within styling makes the active row easier to follow with a keyboard.
- Existing asynchronous recurrence feedback remains available to assistive technology without creating a third visible state.
- Reduced-motion preferences disable switch animation.

## UI polish

- Long entry names truncate cleanly on desktop and wrap safely on mobile.
- Mobile row actions expand consistently and remain easy to tap.
- Focus styles are standardised for workspace tabs, month controls and inline amount editing.
- Prepare Next Month uses Razor-safe variable names, fixing the `@section` directive compilation error.

No database migration is required.
