# Finance Manager Design System

## Principles
1. Reuse shared styles before creating page-specific CSS.
2. Preserve clarity over decoration.
3. Keep important actions visually distinct, but avoid competing primary buttons.
4. Support dark and light themes with semantic colour variables.
5. Every interactive element must have a visible hover and keyboard focus state.

## Foundations
- Font: Inter, with Segoe UI and Arial fallbacks.
- Main content maximum width: 1480px.
- Sidebar: 280px expanded, 84px collapsed.
- Core radii: 12px, 18px and 24px.
- Spacing should use the shared `--space-*` variables where practical.

## Semantic Colours
Use existing CSS variables rather than fixed values:
- `--bg`: page background
- `--card`: primary surface
- `--soft`: secondary surface
- `--ink`: primary text
- `--muted`: secondary text
- `--line`: borders and separators
- `--good`, `--warn`, `--bad`: financial status
- `--brand`, `--brand-2`: branded accents

## Component Rule
No new page should introduce a one-off visual pattern when an existing card, button, badge, form, table or navigation treatment can be reused or extended.
