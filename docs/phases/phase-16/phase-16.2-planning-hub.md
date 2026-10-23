# Phase 16.2 — Planning hub

## Status

Proposed.

## Goal

Present forecasting tools as one planning journey with consistent assumptions and navigation.

## Tabs

- **Forecast** — the live projection based on current finance data.
- **What-if** — temporary changes that do not affect live records.
- **Scenarios** — saved planning inputs and preferred-scenario selection.
- **Compare** — side-by-side scenario results.

## Behaviour

- The selected horizon and contribution assumptions persist between relevant tabs.
- Every result identifies whether it uses live data or a saved/temporary scenario.
- Saving, renaming, duplicating or preferring a scenario does not mutate live finance records.
- Comparison links retain the selected left and right scenarios.

## Acceptance criteria

- Forecast tools are discoverable from one Plan destination.
- Equivalent inputs produce equivalent summary results across Forecast, What-if and Compare.
- Live and hypothetical values are visually and textually distinct.
- Empty scenario states guide the user to create a what-if plan.
- Existing forecast routes remain compatible.

## Out of scope

- Changing the forecast calculation model.
- Automatically applying a scenario to live monthly records.
