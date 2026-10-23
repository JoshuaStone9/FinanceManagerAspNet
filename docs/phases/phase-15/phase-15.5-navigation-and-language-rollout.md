# Phase 15.5 — Navigation and language rollout

## Status

Proposed.

## Goal

Complete the Phase 15 transition by applying the six-destination navigation and consistent language across desktop and mobile surfaces.

## Navigation

The primary sidebar order is:

1. Home
2. Month
3. Accounts
4. Pots
5. Plan
6. Insights

Settings remains in the sidebar footer. Tasks use a global inbox entry or badge. Personal Vault uses an app switch that communicates leaving Finance Manager.

## Language rollout

- Replace ambiguous uses of Investments with either Investment contributions or Holdings.
- Replace ambiguous Money Pots links with Pot funding, Pot goals or Pots according to context.
- Use Accounts for balances and statement ownership rather than generic “money” grouping.
- Use action-led labels such as Set up month, Review statement and Resolve task.
- Remove historical labels after redirects and compatibility help are in place.

## Accessibility and responsive requirements

- Active destination and active tab are independently communicated.
- Collapsed navigation has unique accessible labels and tooltips.
- Mobile navigation closes after a destination is selected.
- Keyboard focus is visible and follows logical document order.
- Badges include accessible text and do not communicate state by colour alone.

## Acceptance criteria

- Only six primary finance destinations appear in the sidebar.
- All former sidebar destinations remain reachable through their owning hub or redirect.
- Navigation works at desktop, intermediate and mobile breakpoints.
- Terminology is consistent in navigation, headings, buttons, empty states and documentation.
- A link audit finds no unintended dead or circular routes.
