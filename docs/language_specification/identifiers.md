# Identifiers **[v0.1.0]**

Identifiers are names for variables, functions, classes, etc.

## Syntax

```
identifier ::= (letter | '_') (letter | digit | '_')*
letter     ::= 'a'..'z' | 'A'..'Z'
digit      ::= '0'..'9'
```

## Rules

- Must start with letter or underscore
- Can contain letters, digits, and underscores
- Case-sensitive: `myVar`, `myvar`, and `MYVAR` are different identifiers
- Cannot be a keyword
- Unicode letters are allowed but discouraged for interop

## Examples

```python
# Valid identifiers
x
my_variable
_private
ClassName
MAX_SIZE
value2
_internal_counter

# Invalid identifiers
2fast      # Cannot start with digit
my-var     # Hyphen not allowed
class      # Keyword
```

## Naming Conventions

| Type | Convention | Example | C# Transformation |
|------|------------|---------|-------------------|
| Local variable | `snake_case` | `user_name` | `userName` (camelCase) |
| Function/method | `snake_case` | `calculate_total` | `CalculateTotal` (PascalCase) |
| Class | `PascalCase` | `UserAccount` | (unchanged) |
| Struct | `PascalCase` | `Vector2` | (unchanged) |
| Interface | `IPascalCase` | `IDrawable` | (unchanged) |
| Constant | `CAPS_SNAKE_CASE` | `MAX_SIZE` | (unchanged) |
| Module | `snake_case` | `user_service` | `UserService` (PascalCase namespace) |
| Enum type | `PascalCase` | `Color` | (unchanged) |
| Enum value | `CAPS_SNAKE_CASE` | `RED` | `Red` (PascalCase) |

## Literal Names (Backtick Escaping)

Any symbol in Sharpy can be surrounded with backticks `` ` `` to tell the compiler not to transform the name during resolution. This is equivalent to C#'s `@` prefix:

```python
# Prevents case transformation
from foo_bar.`abc` import *

# Using a keyword as an identifier
def `class`():
    pass

# Using exact casing without transformation
def `ExactMethodName`():
    pass
```

*Implementation: ✅ Native - Backtick names map to `@name` in C# when needed, or exact casing is preserved.*
