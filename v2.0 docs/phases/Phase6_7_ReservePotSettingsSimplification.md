# Phase 6.7 – Reserve Pot Settings Simplification

Status: Implemented

## Changes
- Renamed **Allocated now** to **Current Balance**.
- Made Current Balance read-only.
- Kept Average Monthly Contribution read-only and clarified that it is calculated from history.
- Made Intended Contribution read-only pending future planning tools.
- Removed Funding Frequency from the interface and business rules.
- Removed Expected Funding Day from the interface and business rules.
- Funding can now be added at any point during the month.
- Current-month status is assessed from monthly totals rather than a specific due day.
- Hidden Notes from the pot settings interface while preserving existing values.
- Pause details are displayed read-only only while the pot is currently paused.
- Simplified the new-pot and edit-pot forms.

## Business Rule
Reserve pots are goal-based and flexibly funded. Contributions made on any day in a month count toward that month's funding total.
