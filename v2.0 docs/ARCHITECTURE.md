# Architecture

Presentation
- Razor Views

Controllers
- Thin orchestration layer

Services
- FundingEngineService
- DashboardSummaryService
- MonthlyFundingReviewService
- ReserveRecommendationService
- RecommendationApplicationService

Repository
- FinanceRepository

Database
- SQL Server (LocalDB during development)

Flow:
UI -> Controller -> Service -> Repository -> Database
