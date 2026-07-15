# Coding Standards

- Controllers coordinate only.
- Business logic belongs in services.
- Repository performs data access only.
- Prefer IReadOnlyList<T> / IEnumerable<T> for read-only parameters.
- Use async APIs.
- Keep funding history immutable.
- Preserve user position after save.
- Use descriptive commit messages.
