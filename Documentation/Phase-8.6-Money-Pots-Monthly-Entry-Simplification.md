# Phase 8.6 — Money Pots & Monthly Entry Simplification

This phase combines the usability corrections identified during the Phase 9 readiness review.

## Monthly entry forms
- Workspace-specific type choices replace generic free-text type fields.
- Time-limited entries can be entered as either a number of months or an end date.
- The end date is converted to the existing month-length value for database compatibility.
- A readable estimated end month is displayed while entering the item.

## Money Pots
- Creation now asks only for name, optional target, optional date needed by and notes.
- Current balance starts at zero and remains transaction-driven.
- Intended contribution, priority, carry-shortfall and carry-excess controls are removed from the active UI.
- Existing database columns remain temporarily for backwards compatibility.
- Average contribution is calculated from actual positive monthly contributions.
- Estimated completion is calculated from current balance, target and recent monthly contribution history.
- Date needed by is an optional comparison date, not the calculated completion date.
- Larger contributions naturally bring the forecast forward; smaller or missed contributions move it later.
- Pause and Resume are explicit actions rather than an ambiguous active checkbox.

## Data interpretation
No negative contribution is automatically created for a missed month because negative transactions represent withdrawals. Forecasts respond directly to actual contribution history instead.
