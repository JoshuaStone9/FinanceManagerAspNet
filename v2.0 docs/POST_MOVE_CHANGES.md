# Post-move Finance Manager changes

This version is designed for use after moving into the house.

## Monthly dashboard
- No forced monthly savings target.
- Remaining money is `income - bills - everyday spending - investments - reserve allocations`.
- The result only turns negative when allocations exceed income.
- Income and each allocation can be entered directly on one page.
- Positive remaining money can be allocated to the household reserve with one button.
- Recurring bills and investments can be copied into the next month.

## Household reserve
- One physical cash-like fund can be held in a money market fund or savings account.
- Virtual pots show how the single balance is reserved without requiring separate bank accounts.
- Each pot supports a current allocation, normal monthly contribution, optional target, optional due date, priority and active/paused state.
- A suggested monthly contribution is calculated when a target and due date are supplied.
- The app warns when virtual allocations exceed the real reserve balance.

## Default virtual pots
- Emergency reserve
- MOT and servicing
- Car insurance
- Car repairs and tyres
- Home repairs
- Wear and tear
- Holidays
- Gifts
- Unallocated reserve

The database tables are created automatically by `EnsureModernTablesAsync` when the application starts.

## V2.6 reserve contribution forecasting
- Reserve pot monthly contribution is recalculated from actual dashboard reserve allocations.
- If a pot has different amounts across months, the value shown is the average monthly total.
- Targeted pots with a due date show the estimated balance by that date and the extra monthly contribution required to reach the target.
