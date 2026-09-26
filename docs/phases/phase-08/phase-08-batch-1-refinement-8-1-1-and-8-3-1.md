# Phase 8 Batch 1 Refinement — 8.1.1 and 8.3.1

## Sidebar interaction

- Expansion and collapse are controlled only by the sidebar button on desktop.
- The control remains available in both expanded and collapsed states.
- The user's preference is stored in local storage and restored on later pages.
- Collapsed navigation uses icon-only items with accessible hover/focus labels.
- The active route remains visible in either state.
- Mobile continues to use an overlay drawer and backdrop rather than desktop collapse behaviour.

## Dashboard month navigation

- Month navigation is a single compact segmented control.
- Previous and next controls use consistent Lucide chevrons and keyboard focus states.
- The selected month is centred and announced through an `aria-live` label.
- On mobile the control moves below the welcome text and fills the available width.
