# Phase 10.7 — Passive Income Engine

The Income workspace now estimates interest for accounts with a positive balance and AER. Estimates are informational until confirmed. Confirming the actual amount creates an `Interest` monthly income entry, so Dashboard and Statistics use only money genuinely received.

Formula: `balance × ((1 + AER / 100)^(1/12) - 1)`.

The provider may calculate interest daily, so the user can overwrite the estimate with the statement amount before confirming it.
