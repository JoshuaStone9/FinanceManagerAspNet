# Sidebar Architecture

The finance shell is rendered in `Views/Shared/_Layout.cshtml` for non-vault controllers.

## State
- Desktop collapse class: `sidebar-collapsed` on `#fmAppShell`.
- Mobile open class: `open` on `#fmSidebar`.
- Persisted key: `fm-sidebar-collapsed`.

## Breakpoints
- Above 1080px: full sidebar unless manually collapsed.
- 761px–1080px: icon-only sidebar.
- 760px and below: off-canvas navigation with mobile header.

## Personal Vault
The Personal Vault retains its specialist internal shell during Batch 1 because it includes category and item-management navigation. A later Phase 8 batch may unify the outer shell while preserving its secondary navigation.
