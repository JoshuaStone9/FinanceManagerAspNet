# Phase 10.2 — Actual-data-only monthly testing

## Purpose

Monthly dashboard and carry-forward calculations now use only income entries explicitly recorded in the Income workspace. Legacy monthly income snapshots, monthly allowance records and the default-income fallback no longer populate an otherwise empty month.

## Behaviour

- A month with no income entries displays £0.00 available income.
- Previous-month comparisons ignore legacy income records.
- Carry-forward calculations use only current monthly income entries.
- The Default household income fallback setting has been removed.
- Settings > Data > Reset finance data clears all finance records, including legacy income, allowance, reserve, account, asset, pot and forecast data, while retaining application settings and the protected reserve baseline.

This makes an empty database visibly empty and allows clean end-to-end testing from the application UI.
