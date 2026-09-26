# Phase 11.3–11.4 — Investment Journey and Interest Tax

## Investment journey
Money Pots can contain ordered stages describing the holding, provider, expected annual return, and date range. Stages can be added, edited, and removed from each pot card.

## Interest tax
Accounts now store tax treatment, tax rate, and an optional future effective date. Reserve interest forecasts show gross interest, estimated tax, and net interest. Tax-free wrappers retain a zero tax deduction.

## Database
The startup schema creates `reserve_pot_investment_stages` and adds tax columns to `account_balances`. Emergency Fund tax settings are held in `finance_settings`.
