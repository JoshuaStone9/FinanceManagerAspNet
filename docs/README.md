# Documentation

[Project overview](../README.md)

## Start here

- [Phase documentation](phases/README.md) — Phases 1–14, with a guide for each documented phase, reading order and historical context.
- [Planning guide](plans/roadmap.md) — proposals and roadmap history, separated from delivered records.
- [Dashboard testing checklist](testing/dashboard-testing-checklist.md) — manual verification.

## Technical reference

These documents include early design context and partial inventories; later phase records describe changes.

- [Product specification](reference/specification.md)
- [Architecture](reference/architecture.md)
- [Database notes](reference/database.md)
- [Coding standards](reference/coding-standards.md)

## UI guidance

- [Design system](ui/design-system.md) — initial baseline with links to later dimension changes.
- [UI standards](ui/ui-standards.md)
- [Navigation guidelines](ui/navigation-guidelines.md)
- [Sidebar architecture](ui/sidebar-architecture.md)
- [Shared components](ui/ui-components.md)

## Historical records

- [Changelog](history/changelog.md) — partial release history ordered by phase.
- [Original V2 overview](history/v2-overview.md)
- [Project overview before this review](history/project-overview-before-documentation-review.md) — preserves the original mixed overview and release notes.
- [Roadmap before this review](history/roadmap-before-documentation-review.md)
- [Post-move changes](history/post-move-changes.md)
- [Personal Vault V2 changes](history/personal-vault-v2-notes.md)
- [Personal Vault V2 extra changes](history/personal-vault-v2-extra-notes.md)

## Maintaining the collection

- Keep the root README focused on the project and setup.
- Put phase briefs, implementation records and refinements together in `phases/phase-NN/`; update that phase’s README.
- Keep superseded or conflicting proposals in the owning phase’s `history/` folder and explain the relationship in its guide.
- Use `plans/` for cross-phase planning and `history/` for cross-phase snapshots.
- Put technical reference in `reference/`, design conventions in `ui/`, and verification checklists in `testing/`.
- Use lowercase hyphenated filenames, padded major phase numbers and unpadded subphases: `phase-06.4-pot-actions.md`. Use `-to-` for ranges and `README.md` for indexes.
- Use relative Markdown links and preserve original phase identifiers. Distinguish a historical status from a verified current feature.
