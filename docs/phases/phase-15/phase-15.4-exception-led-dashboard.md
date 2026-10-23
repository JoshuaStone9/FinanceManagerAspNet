# Phase 15.4 — Exception-led dashboard

## Status

Proposed.

## Goal

Make Home answer two questions quickly: “How is this month going?” and “What should I do next?” Detailed analysis remains in the owning destinations.

## Retained content

- Available money and remaining money for the selected month.
- Overall financial-health state with a short explanation.
- Items requiring attention, ordered by urgency.
- One clear recommended next action.
- A compact monthly breakdown linking to Month.
- Recent activity, limited to a useful small set.

## Content moved out

- Detailed monthly record tables and edit controls.
- Repeated category cards that reproduce Month contents.
- Full pot analysis already available in Pots.
- Full forecast presentation already available in Plan.
- Full statistics and trend presentation already available in Insights.

## Behaviour

- Empty and healthy states should be intentionally calm rather than filled with placeholder cards.
- Each warning links to the exact place where it can be resolved.
- At most one page-level primary action is displayed.
- Historical month selection remains supported, but “today” wording is not shown for historical months.

## Acceptance criteria

- The initial viewport communicates remaining money, health and urgent work.
- Dashboard totals match Month for the same year and month.
- No financial record is edited directly on Home.
- Every dashboard link has one canonical destination.
- The dashboard checklist is updated for the new content model.
