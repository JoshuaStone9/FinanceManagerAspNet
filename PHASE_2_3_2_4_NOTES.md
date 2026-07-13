# Phase 2.3 and 2.4

Implemented smart pause ranges and completed carried-excess presentation.

- Pauses now have a start and end date and apply to every overlapping month.
- If only an end date is entered, the pause starts today.
- Paused months create no new scheduled shortfall.
- Voluntary dashboard contributions remain accepted while paused.
- They clear existing recovery first, then become future credit when enabled, otherwise genuine excess.
- Existing recovery is preserved throughout the pause.
- Funding history distinguishes dashboard money, prior credit used and new credit created.
- Pause date validation prevents an end date before the start date.
