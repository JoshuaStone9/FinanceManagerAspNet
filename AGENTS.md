# Repository guidance

## Commit messages

Use the existing phase-based Conventional Commit style for work belonging to a specific phase:

```text
feat(phase-14.2): add statement reconciliation workspace
fix(phase-14.1): replace automatic balance carry-forward with manual starting adjustments
```

- Use `type(phase-X.Y): concise description`, with lowercase `phase`, a hyphen before the number, and a colon after the scope.
- Use the phase associated with the work; do not invent a phase number or assign unrelated work to the latest phase.
- Omit the scope for project-wide or cross-phase work, for example `docs: organise phase documentation and clarify historical records`.
- Keep descriptions concise, concrete, and in the imperative.
- Preserve phase scopes rather than switching to feature-name scopes by default.

See [coding standards](docs/reference/coding-standards.md) for development conventions.
