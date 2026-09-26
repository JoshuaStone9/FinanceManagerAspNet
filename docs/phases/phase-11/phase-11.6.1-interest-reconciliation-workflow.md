# Phase 11.6.1 — Interest Reconciliation Workflow

Extracted from the Phase 10.4.1 delivery notes, where this later refinement had been appended.


- Removed the separate **Apply pending interest** action from Account Management.
- Interest remains visible in Monthly Income as the original income record only.
- Account Management now handles balance reconciliation exclusively through **Update balance**.
- When pending interest exists, the balance editor defaults to the suggested balance and offers a checked option to mark the existing interest records as reconciled.
- Saving a balance never creates a second income record.
