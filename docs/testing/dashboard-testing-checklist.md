# Dashboard testing checklist

## Financial correctness
- Confirm effective income matches income plus carry forward.
- Confirm each category total matches its detailed table.
- Confirm final remaining matches the existing RemainingFund calculation.
- Confirm extra expenses remain excluded from carry-over.
- Confirm no fixed savings target appears.
- Compare the dashboard forecast card with the full 12-month forecast.

## States
- Test a month with no entries.
- Test a month with negative carry forward.
- Test an over-budget month.
- Test no reminders and multiple reminders.
- Test no target dates and urgent target dates.
- Test large amounts and long names.

## Interaction
- Test every quick-action anchor and page link.
- Add, edit and delete entries and confirm the return anchor is preserved.
- Test collapsed and expanded sidebar states.
- Test keyboard focus and reduced-motion mode.

## Responsive
- Desktop at 1440px and 1024px.
- Tablet around 768px.
- Mobile at 390px and 320px.
