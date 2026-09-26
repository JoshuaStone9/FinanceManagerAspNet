# Phase 8.4.5 — Money Pots

## Purpose

Rebrand the user-facing Household Reserve workspace as **Money Pots** and make the page easier to understand at a glance without changing its underlying financial logic.

## User-facing changes

- Sidebar and page navigation now use **Money Pots**.
- The page header shows the amount saved across active pots.
- Summary cards explain total available money, protected baseline, money available for pots, saved money, remaining target value and unallocated money.
- Backing account and interest forecast language is clearer and less formal.
- Existing pots remain collapsed by default, providing a clean scan of names, balances and status before advanced details are opened.
- Empty-state guidance encourages the user to create their first money pot.
- Create, save and delete actions now use money-pot terminology.

## Technical scope

The rebrand is deliberately UI-facing. Existing types, controller names, repository methods, database tables and event identifiers such as `HouseholdReserve`, `ReservePot` and `SavingPotsController` remain unchanged. This avoids a database migration and keeps existing integrations stable.
