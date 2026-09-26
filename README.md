# Finance Manager V2

An ASP.NET Core MVC application for monthly budgeting, household reserves, Money Pots, account tracking, forecasting and Personal Vault inventory.

## Documentation

Start with the [documentation index](docs/README.md) or [phase guides](docs/phases/README.md). The latest standalone phase document covers [14.2: Statement Reconciliation Workspace](docs/phases/phase-14/phase-14.2-statement-reconciliation-workspace.md).

## Main areas

- Monthly income, bills, everyday spending, extra expenses, investments and Money Pot contributions.
- Dashboard summaries, financial health, trends and Financial Tasks.
- Money Pot contribution history, goals and forecasts.
- Account management, interest handling, tax-aware forecasts and Emergency Fund contributions.
- Statement account linking, PDF storage and manual reconciliation.
- Personal Vault inventory and item records.

Feature records describe the application’s evolution; earlier plans and superseded workflows are labelled in the phase guides.

## Technology

- ASP.NET Core MVC, C# and Razor views.
- SQL Server, with LocalDB used for local development.
- Services and repositories for business logic and persistence.

## Configuration

Configuration is stored within `appsettings.json`.

Example:

```json
{
  "ConnectionStrings": {
    "FinanceManager": "Server=(localdb)\\MSSQLLocalDB2025;Database=Finance_Manager_V2;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```


## Running locally

1. Use a .NET SDK and IDE compatible with the target framework in [the project file](FinanceManagerAspNet.csproj).
2. Restore NuGet packages.
3. Configure the database connection in `appsettings.json`.
4. Follow the applicable database setup or migration notes; many phase records describe automatic startup table creation.
5. Run the application.

## Development guidance

- [Architecture](docs/reference/architecture.md)
- [Coding standards](docs/reference/coding-standards.md)
- [UI guidance](docs/README.md#ui-guidance)
- [Dashboard testing](docs/testing/dashboard-testing-checklist.md)

## Planning and history

- [Planning guide](docs/plans/roadmap.md)
- [Historical changelog](docs/history/changelog.md)
- [Original overview and accumulated release notes](docs/history/project-overview-before-documentation-review.md)
