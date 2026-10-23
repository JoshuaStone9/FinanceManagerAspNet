# Phase 15.0 — Information architecture foundation

## Status

Proposed.

## Goal

Define a stable destination model before changing routes or deleting interfaces. Routine finance work should be organised around user goals rather than individual controllers or database categories.

## Primary destinations

- **Home** — current position, exceptions and next actions.
- **Month** — monthly income, spending, allocations and setup.
- **Accounts** — balances, statements and holdings.
- **Pots** — goals, funding and pot activity.
- **Plan** — forecasts, what-if modelling and saved scenarios.
- **Insights** — statistics, trends and financial history.

Settings remains in the utility area. Personal Vault becomes an app switch rather than a finance destination. Financial Tasks remain accessible globally and are surfaced from Home.

## Required work

- Record every existing sidebar destination and its future owner.
- Define canonical route names for the six destinations.
- Define redirects for routes that will later be retired.
- Add a shared hub-tab pattern that works on desktop, collapsed sidebar and mobile navigation.
- Define the language mappings used by subsequent phases.
- Preserve existing routes during this foundation step.

## Terminology

- Use **Investment contributions** for monthly money assigned to investments.
- Use **Holdings** for current assets and valuations.
- Use **Pot funding** for monthly allocations.
- Use **Pot goals** for targets, balances and configuration.
- Use **Starting adjustment** for a manual amount brought into a month's plan.

## Acceptance criteria

- Every current navigation item has one documented destination.
- No record type has two proposed primary editing paths.
- The six-destination model is usable at all supported breakpoints.
- Old URLs and saved bookmarks have a documented compatibility path.
- No data model, calculation or write workflow changes in this subphase.

## Out of scope

- Removing controllers or database tables.
- Combining actual account balances with virtual pot allocations.
- Redesigning specialist pages before their hub phase.
