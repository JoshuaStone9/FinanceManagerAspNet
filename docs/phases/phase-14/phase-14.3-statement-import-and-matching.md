# Phase 14.3 — Statement import and matching

## Summary
Phase 14.3 develops the statement workspace into a controlled PDF import and reconciliation workflow. Uploaded statements remain outside `wwwroot`; their text is extracted into a preview and no financial data is created until the user confirms the selected rows.

## Delivered capabilities
- Text extraction from uploaded PDF statements using the account's configured parser.
- PDF signature validation in addition to extension and request-size checks.
- Import preview with individual transaction selection.
- Duplicate detection using a stable date, description, amount and direction fingerprint.
- Duplicate protection against both fingerprinted imports and equivalent existing statement lines.
- Controlled import of selected preview rows.
- Ranked match suggestions based on amount, date proximity and description similarity.
- Money-in matching against operating and passive entries recorded in monthly income.
- Creation of a missing Finance Manager record directly from an unmatched statement line, with an editable title and destination section.
- Matching against unassigned monthly records, assigning the confirmed record to the statement account automatically.
- Manual transaction entry remains available for scanned, encrypted or unsupported statements.
- Incorrect unresolved transactions can be deleted.
- Completed statements are locked in the interface and can be explicitly reopened.
- Empty statements cannot be completed.

## Parser behaviour
`Generic`, `FirstDirect`, `Monzo` and `Trading212` use the common text-line parser with provider direction hints where available. `Manual` stores the PDF without attempting extraction. Image-only statements require manual entry because OCR is outside this phase.

## Database additions
- `bank_statement_transactions.import_fingerprint` records the source identity of imported rows.
- `bank_statement_import_rows` stores short-lived preview rows before confirmation.
- A unique statement/fingerprint index prevents duplicate preview rows.

## Security and data integrity
- Imports require authentication and antiforgery validation.
- Files are limited to PDF uploads of at most 10 MB and must start with the PDF file signature.
- Generated internal filenames prevent user-controlled paths.
- Preview rows are scoped to one statement and removed after import.
- Completed reconciliation records require an explicit reopen action before UI editing resumes.

## Verification
- Parser tests cover common debit and credit lines, month filtering and stable fingerprints.
- Match-scoring tests verify that same-date, same-amount and similar-description candidates rank first.
- The full solution build and test suite must pass before release.
