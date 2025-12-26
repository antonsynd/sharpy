# Keywords

## Hard Keywords

The following are reserved keywords in Sharpy:

| Keyword | Notes |
|---------|-------|
| `and` | Boolean AND |
| `as` | Aliasing for imports |
| `assert` | Assertion statement |
| `auto` | Inferred type for shadowing |
| `break` | Break statement for loops |
| `case` | Pattern matching case |
| `class` | Class declaration |
| `const` | Constant declaration |
| `continue` | Continue statement for loops |
| `def` | Function/method definition |
| `elif` | Else-if block |
| `else` | Else block |
| `enum` | Enumeration declaration |
| `event` | Event declaration |
| `except` | Exception handler |
| `False` | Boolean false literal |
| `finally` | Finally block |
| `for` | For loop |
| `from` | Selective imports |
| `if` | Conditional |
| `import` | Import statement |
| `in` | Membership test |
| `interface` | Interface declaration |
| `is` | Identity comparison |
| `lambda` | Lambda expression |
| `match` | Pattern matching |
| `maybe` | Optional from nullable expressions |
| `None` | None/null literal |
| `not` | Boolean NOT |
| `or` | Boolean OR |
| `pass` | No-op placeholder |
| `property` | Property declaration |
| `raise` | Raise exception |
| `return` | Return statement |
| `struct` | Struct declaration |
| `True` | Boolean true literal |
| `to` | Type coercion operator |
| `try` | Try block |
| `type` | Type alias declaration |
| `while` | While loop |
| `with` | Context manager |
| `yield` | Generators |
| `async` | Async programming |
| `await` | Async programming |
| `del` | Delete statement |

## Soft Keywords (Context-Dependent)

| Keyword | Context | Notes |
|---------|---------|-------|
| `_` | Pattern matching | Wildcard pattern (in `case` clauses) |
| `_` | Function call arguments | Partial application placeholder |
| `get` | Properties | Property getter |
| `init` | Properties | Property set-on-initialization only |
| `set` | Properties | Property setter |

**Underscore (`_`) disambiguation:**

The `_` identifier is context-sensitive:

- In `case` pattern positions: wildcard pattern (matches anything, binds nothing)
- In function call argument positions: partial application placeholder
- In assignment targets: regular identifier (conventionally used to discard values)
- In type annotations: regular identifier (not recommended)

See [partial_application.md](partial_application.md) for detailed disambiguation rules.

## Future Keywords

These keywords are not currently used in Sharpy, but are reserved for the
future.

| Keyword | Notes |
|---------|-------|
| `defer` | Deferred execution |
| `do` | Block expression |
