# Combined Finance Manager + Personal Vault

## What changed
- Merged Personal Vault into Finance Manager as one ASP.NET MVC app.
- Front screen button renamed from **Inventory Database** to **Personal Vault** and now opens the vault.
- Added shared top navigation for Dashboard, Statistics, Saving Pots, Personal Vault, and Locations.
- Personal Vault now uses the Finance Manager SQL Server connection string (`ConnectionStrings:FinanceManager`) via EF Core SQL Server.
- Removed the separate Personal Vault login flow. The existing Finance Manager login now controls editing for both apps.
- Anonymous/read-only users can view finance and vault summaries, but cannot create, edit, delete, loan, maintain, manage locations, or export vault data.
- Location filters and item location text are hidden unless logged in.
- Added basic security headers and hardened the auth cookie settings.
- Increased new password minimum from 4 characters to 8 characters.
- Kept the simplified mobile-friendly Personal Vault item form.

## Important setup
The app will create the Personal Vault EF tables in the same SQL Server database used by Finance Manager. Check `appsettings.json`:

```json
"ConnectionStrings": {
  "FinanceManager": "Data Source=STONEYMINI;Initial Catalog=Finance_Manager;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
}
```

Run the project as normal. The first run will call `Database.EnsureCreated()` for the Personal Vault tables.

## Main URLs
- `/` front screen
- `/Dashboard` finance dashboard
- `/SavingPots` saving pots
- `/Items` or `/PersonalVault` personal vault
- `/Locations` manage locations, login required
