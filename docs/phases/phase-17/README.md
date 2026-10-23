# Phase 17 — Internal simplification

[All phases](../README.md) · [Roadmap](../../plans/roadmap.md)

**Status: Proposed.** Phase 17 follows the user-facing route and ownership decisions made in Phases 15 and 16.

## Purpose

Phase 17 reduces maintenance cost and clarifies responsibility boundaries without changing user-visible financial results.

## Documents

- [17.0 — Finance repository decomposition](phase-17.0-finance-repository-decomposition.md)
- [17.1 — Schema migration consolidation](phase-17.1-schema-migration-consolidation.md)
- [17.2 — Controller simplification](phase-17.2-controller-simplification.md)
- [17.3 — Shared view components](phase-17.3-shared-view-components.md)
- [17.4 — Reliability baseline](phase-17.4-reliability-baseline.md)

## Programme constraints

- Refactoring must preserve observable behaviour and database compatibility.
- Controllers coordinate, services own business rules, and repositories perform data access only.
- Funding and transaction history remains immutable except through existing corrective workflows.
- Large rewrites must be split into reversible, independently verified subphases.

## Completion outcome

Data access is organised by domain, startup no longer performs ad hoc schema evolution, controllers accept typed inputs, repeated Razor patterns are shared, and the normal build and test workflow completes reliably.
