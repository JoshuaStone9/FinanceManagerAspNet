# Coding Standards

- Controllers coordinate only.
- Business logic belongs in services.
- Repository performs data access only.
- Prefer IReadOnlyList<T> / IEnumerable<T> for read-only parameters.
- Use async APIs.
- Keep funding history immutable.
- Preserve user position after save.
- Use descriptive commit messages.

## Commit message convention

Use `type(phase-X.Y): concise description` for phase-specific work, matching the existing history. For example:

```text
fix(phase-14.1): replace automatic balance carry-forward with manual starting adjustments
```

Use the phase associated with the change. Omit the scope for project-wide or cross-phase work, such as `docs: organise phase documentation and clarify historical records`. Keep descriptions concise and in the imperative. Prefer phase scopes over feature-name scopes.
