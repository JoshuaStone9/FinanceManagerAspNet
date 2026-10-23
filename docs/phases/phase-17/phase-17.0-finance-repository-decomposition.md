# Phase 17.0 — Finance repository decomposition

## Status

Proposed. Begin only after domain ownership is stable in Phases 15 and 16.

## Goal

Replace the catch-all finance repository with focused data-access components while keeping business rules in services.

## Proposed repositories

- Monthly money and recurring templates.
- Accounts, balances and interest reconciliation.
- Statement reconciliation and import persistence.
- Pots and funding history.
- Forecast scenarios.
- Events and tasks.
- Application settings and authentication data.

The final names should follow the project's interface conventions and may be adjusted during implementation.

## Migration strategy

- Extract one cohesive method group at a time.
- Introduce an interface where it improves testing or establishes a clear boundary.
- Update one consumer group per commit.
- Avoid a compatibility facade that becomes a permanent second catch-all repository.
- Remove migrated methods only after repository-wide reference searches and tests pass.

## Acceptance criteria

- No replacement repository becomes a generic dumping ground.
- Repositories contain SQL and persistence mapping, not financial calculations or UI decisions.
- Existing services and controllers receive only the dependencies they use.
- Data-access results remain equivalent for representative months, accounts and pots.
- The original repository is removed or reduced to an explicitly documented transitional role.

## Out of scope

- Changing database schema solely to make extraction easier.
- Rewriting all ADO.NET access to a different ORM in the same phase.
