# Phase 10.3 — Safe Reset Controls

## Status

Implemented.

## Purpose

Replace the single destructive finance reset with three clearly separated reset levels so test data can be removed without deleting genuine history from other months.

## Reset current month

Uses the server's current month and deletes only operational monthly records for that period:

- Income entries and legacy monthly income summaries
- Essential bills
- Everyday spending
- Extra expenses
- Monthly investment contributions
- Household Reserve / Money Pot allocation entries
- Money Pot monthly funding, actions and recovery allocations connected to that month
- Carry-forward and legacy monthly allowance records

It retains other months, Money Pot definitions, assets, reserve accounts, settings and Personal Vault data.

## Reset selected month

Provides a month picker and applies the same scoped deletion rules to the chosen year and month. This supports clearing test months such as June without affecting genuine July entries.

## Factory reset

Deletes all Finance Manager finance records represented in the backup table set, including monthly history, Money Pots, assets, Household Reserve records, forecasts, events and reminders.

The action requires the exact phrase `DELETE EVERYTHING`. Application settings, security preferences and Personal Vault records are retained.

## Safeguards

- All reset actions require an authenticated editor.
- Every write action uses anti-forgery validation.
- Month values are validated before SQL execution.
- Deletes run inside a SQL transaction and roll back together on failure.
- Month-specific deletes use bounded date ranges or explicit year/month columns.
- The UI explains what is deleted and what remains before submission.
