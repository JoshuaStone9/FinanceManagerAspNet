# Phase 14.2 — Statement Reconciliation Workspace

## Summary
Phase 14.2 introduces the user-facing workspace used to inspect monthly bank statements against financial entries already recorded in Finance Manager. It deliberately separates the reconciliation workflow from PDF parsing so that review, matching and approval behaviour can be tested before automatic extraction is introduced.

## Delivered capabilities
- New **Statements** navigation item.
- Monthly account overview for accounts configured as `StatementOnly` or `Both`.
- One reconciliation record per account and calendar month.
- Secure PDF storage outside `wwwroot` without parsing.
- Manual statement transaction entry.
- Transaction states: `Unmatched`, `Matched`, and `Ignored`.
- Manual matching against account-linked essential bills, everyday spending, extra expenses, investments and money-pot entries.
- Reopen matched or ignored transactions.
- Completion guard that prevents reconciliation while unresolved transactions remain.
- Per-account monthly status and summary counts.

## Database additions
### `bank_statements`
Stores the account/month reconciliation session, optional uploaded file metadata, lifecycle status and completion timestamps. A unique constraint prevents duplicate statement sessions for the same account and month.

### `bank_statement_transactions`
Stores individual statement lines, direction, review status and an optional reference to a matched Finance Manager entry.

## Security
- Editing requires authentication.
- PDF uploads are restricted to `.pdf` files and 10 MB.
- Uploaded files use generated internal filenames.
- Files are stored under `App_Data/statements`, outside the public static-file root.
- Statement contents are not written to application logs.

## Matching behaviour
Only entries linked to the selected account and within the selected calendar month are offered as candidates. Phase 14.2 uses explicit manual matching; no automated confidence score is applied yet.

## Scope exclusions
- PDF text extraction and OCR.
- Provider-specific parsing.
- Automatic matching and confidence scoring.
- Creation or editing of Finance Manager entries directly inside the workspace.
- Income account linking.
- Opening/closing balance verification.

## Acceptance criteria
1. Statement-enabled accounts appear in the monthly overview.
2. Starting a reconciliation creates or reuses one account/month statement record.
3. A PDF can be stored without becoming publicly accessible.
4. Manual statement lines can be created and reviewed.
5. Linked monthly entries appear as match candidates.
6. A statement line can be matched, ignored and reopened.
7. Completion is blocked while any line remains unmatched.
8. Existing account tracking and monthly money functionality remains compatible.

## Next phase
Phase 14.3 will add PDF extraction, provider parsers, import previews, duplicate protection and automatic candidate scoring while continuing to use this workspace for review and approval.
