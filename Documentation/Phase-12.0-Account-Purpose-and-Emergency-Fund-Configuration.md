# Phase 12.0 — Account Purpose and Emergency Fund Configuration

## Purpose

Phase 12.0 removes account-name-based Emergency Fund behaviour. Accounts are classified by a stored purpose, allowing an account to be renamed without changing its financial role.

## Account purposes

- General account
- Emergency fund
- Bills account
- Everyday spending
- Savings
- Investment

The database stores the stable values `General`, `EmergencyFund`, `Bills`, `EverydaySpending`, `Savings`, and `Investment` in `account_balances.purpose`.

## Emergency Fund configuration

Any number of active accounts may use the `EmergencyFund` purpose. Their recorded balances are added together to produce the Emergency Fund total and are compared with `EmergencyFundBaseline`.

One Emergency Fund account is the default contribution destination. New restoration contributions increase that account while the overall shortfall and progress use the combined balance of every Emergency Fund account.

Only one account can be the default destination. If an Emergency Fund account exists without a selected default, the earliest account becomes the default automatically.

## Migration

On startup, an existing balance in the legacy `emergency_fund` table is migrated into a normal account named `Emergency Fund` only when no classified Emergency Fund account already exists. The migrated account is assigned the Emergency Fund purpose and becomes the default contribution destination. Its display name can then be changed safely.

## Ledger behaviour

New Emergency Fund ledger transactions store `account_balance_id`. Reversing a contribution subtracts the amount from the exact destination account used by that contribution. Legacy transactions without an account link remain visible but cannot be reversed automatically.

## UI behaviour

Create Account and Edit Account now include Account purpose and Default Emergency Fund contribution destination controls. The restoration panel is displayed only when the combined Emergency Fund balance is below the configured base level. It is disabled until an Emergency Fund destination account is available.
