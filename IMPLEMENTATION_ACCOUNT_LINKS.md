# Monthly entry account links

Implemented optional account assignment for essential bills, everyday spending, extra expenses, investments and money-pot contributions.

## Behaviour
- Each monthly quick-entry form now includes an optional Account dropdown.
- Existing entries can be assigned or changed from the Edit screen.
- Assigned account names appear underneath recorded entries.
- Existing records remain valid because account assignment is nullable.
- Direct month carry-over retains the assigned account where applicable.
- Startup database maintenance adds `account_balance_id` to the five monthly payment tables automatically.

## Scope
Income account assignment and PDF statement parsing are not included in this phase. The payment-account link is the prerequisite for later statement matching.
