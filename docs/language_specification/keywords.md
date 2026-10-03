# Keywords

## Hard Keywords

The following are reserved keywords in Sharpy:

| Keyword | Notes |
|---------|-------|
| `and` | Boolean AND |
| `as` | Aliasing for imports |
| `assert` | Assertion statement |
| `auto` | Deprecated — use `let` (retired in Stage 2, #1974) |
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
| `let` | Fresh block-scoped binding (local variable declaration) |
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
| `del` | Refused (SPY0144); see [del_statement.md](del_statement.md) |
| `delegate` | Delegate type declaration |
| `super` | Base class method access |
| `union` | Union type declaration |
| `Self` | Self-referential type annotation |

### Using a keyword as a name

A hard keyword cannot name a variable, field, parameter or function. Written where a name is
introduced, it is refused (SPY0101) with a diagnostic that names the keyword and its escape: write
the keyword in backticks (see [identifiers.md](identifiers.md#literal-names-backtick-escaping)),
or rename it. The escaped name is emitted verbatim — `` `type` `` is the C# name `type`, not
`Type`.

```python
class MoveData:
    `type`: int = 0             # `type: int = 0` is SPY0101: 'type' is a reserved word; write `type` in backticks to use it as a name, or rename it

def describe(`class`: str) -> str:
    return "class " + `class`

def main():
    m = MoveData()
    m.`type` = 3
    print(m.`type`)             # 3
    print(describe("A"))        # class A
```

## Reserved Rejected Keywords

The following Python keywords are reserved by the lexer and produce a compile-time error if used. Sharpy uses C# scoping rules instead.

| Keyword | Notes |
|---------|-------|
| `global` | C# scoping rules apply; `global` is not needed |
| `nonlocal` | C# scoping rules apply; `nonlocal` is not needed |

## Soft Keywords (Context-Dependent)

| Keyword | Context | Notes |
|---------|---------|-------|
| `_` | Pattern matching | Wildcard pattern (in `case` clauses) |
| `_` | Function call arguments | Partial application placeholder |
| `get` | Properties | Property getter |
| `init` | Properties | Property set-on-initialization only |
| `set` | Properties | Property setter |
| `add` | Events | Event `add` accessor ([events.md](events.md)) |
| `remove` | Events | Event `remove` accessor ([events.md](events.md)) |
| `before_set` | Properties | Property observer run before the store (experimental, `property_observers`) |
| `after_set` | Properties | Property observer run after the store (experimental, `property_observers`) |
| `when` | `except` clauses | Exception filter ([exception_handling.md](exception_handling.md)) |
| `out` | Parameters and call arguments | Output parameter modifier ([parameter_modifiers.md](parameter_modifiers.md)) |
| `ref` | Parameters and call arguments | Reference parameter modifier ([parameter_modifiers.md](parameter_modifiers.md)) |
| `out` | Type parameter lists | Covariance ([generic_variance.md](generic_variance.md)) |
| `notnull` | Type parameter constraints | `T: notnull` ([generics.md](generics.md)) |
| `new` | Type parameter constraints | `T: new()` ([generics.md](generics.md)) |

Outside its context a soft keyword is an ordinary identifier. **Backtick-escaping a soft keyword
makes it an ordinary identifier at every one of these sites too** (see
[identifiers.md](identifiers.md#literal-names-backtick-escaping)): `` f(1, `_`) `` passes the
variable `_` rather than creating a partial application, `` case `_`: `` binds a local named `_`
rather than matching as a wildcard, and `` property `get` name(self) `` declares a property named
`get` (followed by a stray token — a parse error). An escaped `_` is a readable local in every
binding position, including pattern captures and tuple-unpacking targets.

```python
def add(a: int, b: int) -> int:
    return a * 10 + b

def main():
    _ = 3
    print(add(1, `_`))      # 13 — the variable `_`, not a placeholder
    match 5:
        case `_`:            # binds `_`; not the wildcard
            print(`_`)       # 5
    match [1, 2, 3]:
        case [1, *`_`]:
            print(`_`)       # [2, 3]
        case _:              # bare `_`: the wildcard
            print("other")
```

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
