# Phase 17.4 — Reliability baseline

## Status

Proposed.

## Goal

Finish the simplification programme with a dependable verification baseline for navigation, monthly workflows, reconciliation and financial calculations.

## Automated coverage

- Canonical and legacy route behaviour.
- Month setup idempotency and duplicate protection.
- Monthly total consistency between Home, Month and Insights.
- Account and statement reconciliation state transitions.
- Pot funding, withdrawal and recovery invariants.
- Forecast equivalence for live, temporary and saved inputs.
- Authentication and antiforgery protection for write actions.

## Build and dependency work

- Ensure the normal solution build and test commands complete without hanging.
- Resolve or explicitly document package vulnerability warnings.
- Remove obsolete package references and generated-project exclusions where no longer required.
- Keep generated statement files and render artifacts outside source control.

## Manual verification

- Smoke-test the six primary destinations at desktop and mobile widths.
- Verify read-only and authenticated modes.
- Verify dark and light themes.
- Follow every dashboard exception and task deep link.
- Exercise redirects from retired routes and saved bookmarks.

## Acceptance criteria

- The documented build and test commands complete successfully from a clean checkout.
- Critical workflows have regression coverage proportional to financial risk.
- No known high-severity dependency warning remains without a documented decision.
- The working tree remains clean after build and test execution.
- Release verification results are recorded in the relevant delivered phase documents.
