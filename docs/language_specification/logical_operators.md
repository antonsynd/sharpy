# Logical Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `and` | Logical AND (short-circuit) | `&&` |
| `or` | Logical OR (short-circuit) | `\|\|` |
| `not` | Logical NOT | `!` |

## Truthiness

Operands of `and`, `or`, and `not` are evaluated for **truthiness** — the same rule that governs `if`, `while`, `assert`, ternary conditions, comprehension filters, and match guards. A type is truth-testable when it has a falsy case:

| Type | Truthiness lowering | Falsy value |
|------|-------------------|-------------|
| `bool` | identity | `False` |
| `int` | `x != 0` | `0` |
| `float` | `x != 0.0` | `0.0` |
| `long` | `x != 0L` | `0L` |
| `str` | `x.Length > 0` | `""` |
| `bytes` | `((ISized)x).Count > 0` | `b""` |
| collections (`list`, `dict`, `set`, `tuple`) | `((ISized)x).Count > 0` | empty |
| `None` | `false` (always) | `None` |
| `Optional[T]` | `x.IsSome` | `None()` |
| `T?` (nullable) | `x != null` | `None` |
| UDT with `__bool__` | `x.IsTrue` | implementation-defined |
| UDT with `__len__` | `((ISized)x).Count > 0` | empty |
| objects, functions, delegates | **refused** (SPY0220) | no falsy case |

**Deviation from Python:** Python makes objects without `__bool__`/`__len__` vacuously truthy. Sharpy refuses them — the check can never do anything useful.

## Return Type

`and` and `or` return `bool`, not the operand value. This is a deliberate deviation from Python, where `"" or "Anonymous"` returns `"Anonymous"`. In Sharpy, `"" or "Anonymous"` evaluates to `True`. See `null_coalescing_operator.md` for the `??` operator that provides the Python-style fallback behavior.

## Examples

```spy
x: int = 42
if x and x > 0:
    print("positive")

name: str = ""
result: bool = name or "fallback"  # True (both sides truth-tested)

xs: list[int] = [1, 2, 3]
print(not xs)  # False (non-empty list is truthy)
```
