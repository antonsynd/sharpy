# Code Review

Review code changes for the Sharpy compiler. **Read-only analysis.**

## Target

$ARGUMENTS

## Review Criteria

### Security
- Input validation for compiler inputs (source files, configs)
- No hardcoded secrets, no unsafe deserialization
- Flag outdated NuGet packages with CVEs

### Performance
- Minimize allocations in hot paths (`Span<T>`, `ArrayPool<T>`)
- Avoid sync-over-async; use `ConfigureAwait(false)` in libraries
- `Any()` over `Count() > 0`; avoid multiple LINQ enumerations

### SOLID & .NET Conventions
- SRP, DI via constructor injection, depend on abstractions
- PascalCase public, `_camelCase` private fields
- Proper nullable annotations, `using` for disposables
- XML docs on public APIs

### Sharpy Design Alignment

Sharpy is **.NET first, Pythonic second**:
- Static typing, compile-time resolution, .NET type system
- Pythonic syntax (`list[T]`, snake_case) compiling to idiomatic .NET
- No dynamic dispatch, monkey patching, or runtime type discovery

### Change Scope
- No unnecessary churn; localized changes over sweeping refactors
- Split unrelated formatting into separate PRs

## Output Format

Provide feedback organized by severity:
1. **Critical** - Must fix before merge
2. **Warning** - Should fix, potential issues
3. **Suggestion** - Nice to have improvements

Include specific line references and code suggestions where applicable.
