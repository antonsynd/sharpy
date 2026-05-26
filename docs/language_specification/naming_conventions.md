# Naming Conventions for Symbols

| Identifier Type | Sharpy Convention | Compiled C# Form |
|-----------------|-------------------|------------------|
| Module | `snake_case` | `PascalCase` namespace |
| Class | `PascalCase` | (unchanged) |
| Struct | `PascalCase` | (unchanged) |
| Interface | `IPascalCase` | (unchanged) |
| Method/Function | `snake_case` | `PascalCase` |
| Parameter | `snake_case` | `camelCase` |
| Local variable | `snake_case` | `camelCase` |
| Constant | `CAPS_SNAKE_CASE` | (unchanged) |
| Enum type | `PascalCase` | (unchanged) |
| Enum value | `CAPS_SNAKE_CASE` | `PascalCase` |
| Tagged union type | `PascalCase` | (unchanged) |
| Tagged union case | `PascalCase` | (unchanged) |

## Compile-Time Enforcement

Naming conventions are enforced at compile time via `SPY0453` warnings. Identifiers that don't match the expected convention for their kind produce a warning with a suggested name.

### Exemptions

The following identifiers are exempt from convention checks:

- **Backtick-escaped identifiers** — e.g., `` `MySpecialName` `` bypasses all naming checks
- **Dunder names** — e.g., `__init__`, `__str__`, `__add__` are exempt
- **`self` and `cls` parameters** — these are language-level keywords exempt from convention checks

For the complete name transformation algorithm, including edge cases, collision handling, and dunder method mappings, see [name_mangling.md](name_mangling.md).
