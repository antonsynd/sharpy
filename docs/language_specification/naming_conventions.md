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

For the complete name transformation algorithm, including edge cases, collision handling, and dunder method mappings, see [name_mangling.md](name_mangling.md).
