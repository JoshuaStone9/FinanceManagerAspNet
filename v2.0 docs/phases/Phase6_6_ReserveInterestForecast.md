# Phase 6.6 – Reserve Interest Forecasting

## Status
Implemented.

## Objective
Allow the user to project interest on the complete balance of the selected Household Reserve accounts to any chosen future date.

## Delivered
- User-selected forecast date.
- Optional inclusion of configured future monthly account contributions.
- Separate calculation for every selected reserve account using its own AER.
- Combined current balance, future contributions, estimated interest and projected balance.
- Per-account forecast breakdown.
- Forecasting is read-only and does not update live balances, pots or funding history.

## Calculation rules
- Opening balances grow using each account's annual effective rate over the exact forecast period.
- Optional monthly contributions are added monthly and earn interest from their contribution date.
- Negative balances, rates and monthly contributions are treated as zero for forecast safety.
- A forecast date earlier than today is normalised to today.

## Technical design
- `IReserveInterestForecastService`
- `ReserveInterestForecastService`
- `ReserveInterestForecast`
- `ReserveAccountInterestForecast`

## Completion criteria
- Selected accounts are forecast independently.
- Results combine correctly into a total.
- The selected date can be changed freely.
- Forecasting never changes stored financial data.
