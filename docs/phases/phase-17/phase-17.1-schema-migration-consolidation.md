# Phase 17.1 — Schema migration consolidation

## Status

Proposed.

## Goal

Make database migrations the single, inspectable mechanism for schema evolution and remove ad hoc startup schema alteration.

## Required work

- Inventory every `CREATE TABLE`, `ALTER TABLE`, index and constraint operation performed outside migrations.
- Compare the migration model with databases produced by historical startup logic.
- Add migrations for missing schema state without recreating existing objects destructively.
- Define the supported upgrade path for an existing local database.
- Move lookup seeding to an explicit, idempotent mechanism.
- Remove obsolete schema functions from application startup after upgrade verification.

## Safety requirements

- Take or instruct a backup before testing upgrades against meaningful local data.
- Avoid dropping columns or tables unless a separate data-retention decision authorises it.
- Make migrations safe for both a fresh database and the documented supported existing state.
- Fail startup with an actionable message when migration cannot proceed safely.

## Acceptance criteria

- A fresh database can be created from migrations.
- A representative existing database upgrades without data loss.
- Normal startup contains no hand-written schema-alteration block.
- Migration status is observable and documented.
- Automated verification covers fresh creation and supported upgrade state where practical.
