# Phase 2 – Funding Engine

## Objectives
Introduce automated monthly funding tracking.

## Features
- Monthly funding records
- Funded / Partial / Missed / Overdue states
- Recovery balances
- Genuine excess
- Carried excess
- Funding timeline
- Funding history

## Architecture Impact
Funding engine introduced.

## Database Changes
- Monthly funding tables
- Funding history

## Services
- FundingEngineService

## UI
- Funding timeline
- Monthly funding views

## Business Rules
- Funding history is immutable.
- Recovery is calculated automatically.

## Testing
Funding scenarios including partial, missed and excess.

## Completion Criteria
Implemented
