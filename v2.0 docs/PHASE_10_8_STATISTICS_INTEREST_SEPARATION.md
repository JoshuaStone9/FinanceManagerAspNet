# Phase 10.8 — Statistics Interest Separation

Statistics now treats confirmed interest and future interest as separate concepts.

## Forecast behaviour

- Current account balances remain the source of truth and already include any interest that has been credited and reflected in those balances.
- Confirmed income entries categorised as `Interest` are removed from completed-month results before the operating-surplus average is calculated.
- The operating-surplus forecast therefore reflects ordinary income, spending and allocations rather than repeatedly forecasting past interest.
- Future interest is calculated separately from current included-account balances, saved annual rates, regular account contributions and forecast operating-surplus contributions.

## Statistics presentation

The page now shows:

- average operating surplus;
- interest received across the completed months used by the forecast;
- total interest received in the current year;
- expected unconfirmed interest for the current month;
- projected future interest through the selected goal date;
- a per-month explanation of any interest removed from the operating result.

This prevents confirmed interest from being counted once in historical monthly surplus and again as future compound interest.
