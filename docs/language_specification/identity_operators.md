# Identity Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `is` | Identity comparison | `object.ReferenceEquals()` |
| `is not` | Negated identity | `!object.ReferenceEquals()` |
| `is None` | None check | `x is null` (reference / `T \| None`), `(object)x is null` (value type), `.IsNone` (Optional) |
| `is not None` | Non-None check | `x is not null` (reference / `T \| None`), `(object)x is not null` (value type), `.IsSome` (Optional) |

## `is None` Never Runs User Code

`is None` / `is not None` are a reference null check on every type — a Sharpy class with
`__eq__`, a .NET class with an overloaded `==`, `str`, a collection. They lower to the C# null
pattern, which calls no operator. `==` is the spelling that runs `__eq__` (or a .NET
`operator ==`):

```python
class Anything:
    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 0


def main() -> None:
    a: Anything | None = Anything()
    print(a is None)      # False — a holds a live object
    print(a == None)      # True — `==` runs __eq__, as in Python
    b: Anything | None = None
    print(b is None)      # True
    print(b == None)      # True — a None left operand is never dereferenced
```

A .NET type whose `==` treats a live object as null (Unity's destroyed `Object`) answers `False`
to `is None`; see [.NET Interop](dotnet_interop.md#testing-a-net-reference-for-none) for the
spelling that asks its `==`.

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
