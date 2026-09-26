# Phase 9 — Settings & Application Configuration

Phase 9 introduces a dedicated application control centre and removes the former Settings placeholder.

## 9.1 Finance

- Protected emergency-fund baseline, defaulting to £12,000.
- Default monthly-income fallback.
- Money Pot forecast calculation preference.
- Currency and pence-display preferences.
- Recurring month-preparation and completed-pot defaults.
- The configured emergency baseline now feeds the shared reserve-account summary used by Dashboard, Money Pots and Forecast.

## 9.2 Appearance

- Default theme and accent preference.
- Compact mode.
- Default sidebar state.
- Browser preferences remain user-overridable through the existing theme/sidebar controls.

## 9.3 Security

- Remember-login duration.
- Session-timeout policy value for future enforcement.
- Read-only-when-logged-out preference.
- Owner password change.

## 9.4 Data & Backup

- JSON snapshot download.
- JSON snapshot restore.
- CSV export of monthly income and expenditure.
- Confirmed finance-data reset which retains application settings and the protected baseline.

Backups are intentionally limited to finance-domain tables. Personal Vault media and uploaded files remain outside this finance snapshot.

## 9.5 About & diagnostics

- Application and database version information.
- Database connectivity, table count and settings count.
- System-health status.

No manual migration is required because the existing `finance_settings` key/value table stores the new configuration values.
