# Identity Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `is` | Identity comparison | `object.ReferenceEquals()` |
| `is not` | Negated identity | `!object.ReferenceEquals()` |
| `is` / `is not` on an enum | Member identity | `==` / `!=` (same enum), `object.Equals()` (otherwise) |
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

<!-- spec-sweep: error SPY0349 -->
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

## Identity on Enums

Enum members are singletons, so `is` on an enum answers what `==` on its members answers
(owner ruling 2026-11-12, #2669). Whenever an operand's static type is an enum, `is` / `is not`
compare the members themselves:

```python
enum Color:
    RED = 1
    GREEN = 2

enum Size:
    S = 1


def main() -> None:
    c: Color = Color.RED
    d: Color = Color.RED
    print(c is d)              # True — the same member
    print(c is Color.GREEN)    # False
    print(c is not Color.RED)  # False
    absent: Color | None = None
    print(absent is c)         # False — None is not a member
    anything: object = c
    print(anything is c)       # True — the object holds that member
    s: Size = Size.S
    print(c is s)              # False — members of different enums are never identical
```

- Both operands of the same enum type (either may be `E | None`) lower to the enum's own
  `==` / `!=`; with `E | None` a `None` is identical only to another `None`.
- An enum against any other operand — `object`, another enum type — lowers to
  `object.Equals()`: both operands are evaluated exactly once, and members of two different
  enums are never identical, even with equal values.
- A member written after `is` (`c is Color.RED`) is a value, not a type name, so it is this
  comparison and not the retired type test; `c is Color`, naming the enum type itself, is still
  SPY0349. This holds for a Sharpy enum (top-level, nested or module-qualified) and a top-level
  CLR enum (`d is DayOfWeek.Monday`). A member of a CLR enum nested in another type
  (`f is Environment.SpecialFolder.Desktop`) is still read as a type and refused SPY0349
  (#2681); compare it with `==`.
- An enum operand never draws the SPY0465 boxing warning below.

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
- *✅ Enum operands: native `==` / `!=` for one enum type, `object.Equals()` otherwise (#2669).*
- *⚠️ SPY0465 warning when both operands are value types (never for an enum operand).*
