# Architecture

> Architecture overview with early forecast notes. The final reference to future scenario persistence predates the saved scenarios described in [Phase 7](../phases/phase-07/README.md).

Presentation
- Razor Views

Controllers
- Thin orchestration layer

Services
- FundingEngineService
- DashboardSummaryService
- MoneyPotActivityService
- ReserveRecommendationService
- RecommendationApplicationService
- FinancialForecastService (pure, read-only calculation service)

Repository
- FinanceRepository

Database
- SQL Server (LocalDB during development)

Flow:
UI -> Controller -> Service -> Repository -> Database


## Forecast architecture

`ForecastController` gathers selected reserve accounts and active reserve pots, creates a `FinancialForecastRequest`, and passes it to `IFinancialForecastService`. The service is deterministic and has no repository dependency, which keeps calculations independent from MVC and SQL Server and allows direct unit testing. The resulting `FinancialForecastResult` is rendered by the Forecast Razor view.

Flow:
Current data -> ForecastController -> FinancialForecastService -> Forecast result -> Razor view

No forecast calculation writes to the database. Future scenario persistence must remain separate from live finance records.
