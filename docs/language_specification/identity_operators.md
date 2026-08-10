# Identity Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `is` | Identity comparison | `object.ReferenceEquals()` |
| `is not` | Negated identity | `!object.ReferenceEquals()` |
| `is None` | None check | `== null` (nullable/reference), `.IsNone` (Optional) |
| `is not None` | Non-None check | `!= null` (nullable/reference), `!.IsNone` (Optional) |

## `is` Is Not a Type Test

`is` compares references, never types. Writing a type name on the right — `x is Dog` — is
rejected with **SPY0349**; it was a type test in earlier versions of Sharpy, which silently
diverged from CPython (`a is Dog` in CPython is an identity comparison against the class object,
so it is `False`). Use `isinstance()` to test a value's type.

```python
class Dog:
    name: str

    def __init__(self, name: str):
        self.name = name


def main() -> None:
    x: object = Dog("Rex")
    if x is Dog:          # SPY0349: 'is' compares references, not types
        print("never")
```

```
error[SPY0349]: 'is' compares references, not types. Use 'isinstance(x, Dog)' to test a value's type.
```

The `isinstance()` spelling compiles and narrows `x` to `Dog` inside the branch:

```python
class Dog:
    name: str

    def __init__(self, name: str):
        self.name = name


def main() -> None:
    x: object = Dog("Rex")
    if isinstance(x, Dog):
        print(x.name)     # prints: Rex
```

The identity spellings in the table above are unaffected: `x is None`, `x is not None` and
`x is y` between two references all keep working, because none of them names a type.

## Value-Type Boxing Warning

Using `is` or `is not` with value types (e.g., `int`, `bool`, `float`, structs) emits a
compile-time warning (**SPY0465**) because identity comparison on value types is meaningless
in .NET: each operand is boxed into a separate heap object, so the result is always `False`.
Use `==` or `!=` for value equality instead.

```python
x: int = 1
y: int = 1
x is y    # SPY0465 warning — always False due to boxing
x == y    # correct — value equality
```

*Implementation*
- *✅ Native for None checks; 🔄 Lowered for general identity.*
- *⚠️ SPY0465 warning when both operands are value types.*
