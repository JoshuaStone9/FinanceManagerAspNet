# V2 funding tracking update

Implemented:
- Funding plan start date with imported baseline (first positive dashboard contribution, falling back to 1 January 2026).
- Live Pending, Overdue, In progress, Partially funded, Missed, Funded, Overfunded, Paused and Inactive statuses.
- Persistent monthly funding records sourced from dashboard Household Reserve allocations.
- Carry-forward recovery calculation, with later excess contributions reducing the outstanding amount.
- Monthly funding checklist and attention summary.
- Expected balance today, missed/partial month counts and target projections.
- Recommended recovery allocation from currently unallocated reserve funds (recommendation only).
- Pot funding history table.
- Recalculate funding history control.
- General Finance Events table, page and pot-filtered Events buttons.
- Global page-position preservation after POST forms, plus pot anchors after save.

Important behaviour:
- Pausing keeps an allocation active and suppresses the current expected contribution until the pause date.
- Unticking Active disables ongoing tracking for the allocation.
- Irregular pots do not generate fixed monthly missed amounts.
- Existing dashboard savings rows are the contribution source; no duplicate contribution ledger was introduced.
