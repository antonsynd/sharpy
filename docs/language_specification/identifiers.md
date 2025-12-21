# Identifiers

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

See [naming_conventions.md](naming_conventions.md) for naming conventions of various symbols.

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

*Implementation*
- *✅ Native - Backtick names map to `@name` in C# when needed, or exact casing is preserved.*
