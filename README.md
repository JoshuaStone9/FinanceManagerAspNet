# Finance Manager ASP.NET Version

This is a mobile-friendly ASP.NET Core MVC version of the uploaded WinForms Finance Manager.

## What it keeps compatible

It reads your existing SQL Server database `Finance_Manager` and keeps the existing table names used by the WinForms app:

- `dbo.bills`
- `dbo.extra_expenses`
- `dbo.investments`
- `dbo.savings`
- `dbo.emergency_fund`
- `dbo.monthly_allowance`

The dashboard calculations use the same core approach:

`remaining fund = monthly allowance - bills - extra expenses - investments + savings`

The monthly saving target is set to `£1,200`, and the global goal is set to `£20,000`.

## New optional tables

On startup/use, the repository creates small extra tables if missing:

- `finance_settings` for hidden/changeable settings such as interest rate
- `account_balances` for Lucy's ISA, Monzo pots and future pots/accounts
- `account_balance_history` for update pattern/statistics
- `monthly_income_stats` for manual money-in and sick day tracking

These are additive and do not replace your existing data.

## How to run

1. Open the folder in Visual Studio 2022.
2. Restore NuGet packages.
3. Check `appsettings.json` connection string.
4. Run the project.

You can also set an environment variable named `FM_CONNECTION_STRING` to override the connection string, matching your old app pattern.

## Main features added

- Phone-friendly dashboard.
- Month navigation with left/right arrows.
- Red/green carry-over result before each month.
- £1,200 monthly saving target.
- £20,000 global goal including emergency fund and extra accounts.
- Interest forecast for emergency fund, Lucy's ISA and Monzo pots.
- Hidden-ish advanced interest fields inside the account modal.
- Statistics page for income, sick days, account updates and goal estimates.
- One carry-over button with checklist sections for bills, investments and optional extra expenses.

## Interest forecast update

The Statistics page now calculates projected balances using monthly compounding:

1. Current account balance is taken from `emergency_fund` and `account_balances`.
2. Monthly contribution is added each month.
3. Monthly interest is applied using `annual rate / 12`.
4. The page shows normal projected money, projected money with interest, interest earned, and the estimated goal month.

The carry-over SQL has also been simplified to avoid INSERT column/value count errors.

## Latest update

This version includes:

- STONEYMINI SQL Server connection string in `appsettings.json`.
- Fixed carry-over SQL using `INSERT ... SELECT`.
- Add payment / bill dashboard flow.
- Interest forecast statistics.
- Goal forecast now adds the planned £1,200 monthly saving target into the £20,000 projection, not just the current pots and interest.

## Latest update - House / Moneybox forecast

Added a Statistics page House planner with:

- Manual external total value box.
- £30,000 house goal input.
- Moneybox fund amount input.
- Moneybox interest rate input.
- Bonus input that is taken off the total needed.
- Normal remaining amount and remaining amount with Moneybox interest.
- Interest-only calculator for any custom amount/rate/months.
- The £1,200 monthly saving target is applied to the house/Moneybox forecast only and is not added into the Emergency Fund account projection.
