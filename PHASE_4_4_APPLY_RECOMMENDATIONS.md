# Phase 4.4 - Apply Recommendations

Implemented deliberate application of recovery recommendations.

## Included

- Apply one recommendation from the Household Reserve page.
- Apply all recommendations in priority order.
- Confirmation prompts before changes are submitted.
- Server-side recalculation before applying, so stale displayed values cannot over-allocate money.
- Serializable SQL transaction and operation-key idempotency protection.
- Recommendation amount capped by current unallocated reserve and current outstanding recovery.
- A one-off savings contribution record is created for funding history.
- The selected virtual pot allocation increases while the physical Household Reserve balance remains unchanged.
- Funding history and contribution averages are rebuilt after application.
- Finance event recorded as `RecoveryRecommendationApplied`.
- Related funding reminder completed when recovery is cleared.
- Related target reminder completed when the application reaches the target.
- Redirect returns to the recommendations section.

## Accounting behaviour

Applying a recommendation moves money from the reserve's unallocated portion into a virtual pot. It does not add new money to the physical reserve balance.
