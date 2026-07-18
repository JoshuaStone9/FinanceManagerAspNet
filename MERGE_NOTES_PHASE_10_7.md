# Phase 10.7 merge notes

Use this ZIP as a clean project replacement rather than extracting it over an older project folder. Old files that are no longer present are not deleted by normal ZIP extraction.

After replacement, remove local `.vs`, `bin`, and `obj` folders before rebuilding.

The passive-income table is created automatically by `FinanceRepository.EnsureModernTablesAsync()` at startup.
