# Phase 11.6.4 – Editable Interest Handling

Received interest can now have its handling corrected while it is still pending.

## Behaviour

- Interest recorded as **Keep invested** remains editable until it is included in an account balance reconciliation.
- Changing a pending payment to **Add to Monthly Income** creates the linked monthly income entry once and then locks the choice.
- Interest already added to monthly income is locked.
- Interest already applied during an account balance update is locked.
- Server-side checks enforce the lock even if a request is submitted manually.
- The update runs in a database transaction to prevent duplicate income entries.

## UI

Received-interest rows display **Edit handling** only while the payment remains eligible. Locked rows explain whether the interest has already been added to income or applied to an account balance.
