# Phase 17.2 — Controller simplification

## Status

Proposed.

## Goal

Keep controllers focused on HTTP concerns and orchestration by moving validation policies and business operations into typed services.

## Required work

- Replace long action parameter lists with purpose-specific input models.
- Centralise selected-month validation and canonical return-route handling.
- Use a shared edit-authorisation policy rather than repeating authentication checks inconsistently.
- Move transaction orchestration and financial decisions into application services.
- Keep TempData messages and HTTP result selection in controllers.
- Standardise not-found, invalid-input and read-only responses.

## Design rules

- Input models define validation attributes and normalisation boundaries.
- Services return explicit results rather than relying on exceptions for expected validation failures.
- Redirect destinations preserve selected month, tab and relevant record focus.
- POST actions remain protected by antiforgery validation.

## Acceptance criteria

- Controllers no longer contain financial calculation logic.
- Repeated authentication, month-validation and redirect code is materially reduced.
- Complex write workflows have direct service tests.
- Invalid input produces consistent user-facing feedback.
- Route compatibility from Phases 15 and 16 is retained.
