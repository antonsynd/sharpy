# Membership Operators

| Operator | Description |
|----------|-------------|
| `in` | Membership test |
| `not in` | Negated membership |

<!-- spec-sweep: fragment -->
```python
if item in collection:
    print("Found")
```

## Dispatch Priority

The `in` operator always emits a `.Contains()` call on the right-hand operand:

```
x in y    -->  y.Contains(x)
x not in y --> !y.Contains(x)
```

This single codegen strategy works uniformly because:

1. **User-defined classes with `__contains__`**: The `__contains__` dunder compiles to a C# method named `Contains` (via `DunderNameMapping`). So `x in obj` emits `obj.Contains(x)`, which resolves to the user's `__contains__` implementation.
2. **Sharpy built-in collections** (`list`, `dict`, `set`): These Sharpy.Core types define a `Contains` method that implements the membership test.
3. **.NET types** (`HashSet<T>`, `List<T>`, etc.): These already have a `.Contains()` method from `ICollection<T>` or similar interfaces.
4. **Strings**: `str.Contains()` performs a substring test.

### Validation

The `ProtocolValidator` checks that the right-hand operand of `in`/`not in` supports membership testing. For user-defined types, the class must define `__contains__`. For .NET types, the validator checks for a `Contains` method via CLR interface discovery (`ICollection<T>`, `IDictionary<K,V>`, etc.).

If the type does not support membership testing, the compiler reports `SPY0320` ("Type does not support membership testing (missing `__contains__` method)").

### `__contains__` Protocol

| Property | Value |
|----------|-------|
| Dunder name | `__contains__` |
| Protocol kind | Container |
| C# method name | `Contains` |
| Expected parameters | 2 (self, item) |
| Expected return type | `bool` |

### Examples

```python
# User-defined class with __contains__
class Bag:
    _items: list[str]

    def __init__(self):
        self._items = []

    def add(self, item: str) -> None:
        self._items.append(item)

    def __contains__(self, item: str) -> bool:
        for stored in self._items:
            if stored.lower() == item.lower():
                return True
        return False

def main():
    bag: Bag = Bag()
    bag.add("Hello")
    print("hello" in bag)       # True (case-insensitive via __contains__)
    print("missing" not in bag) # True

# Built-in collections
items: list[int] = [1, 2, 3]
if 2 in items:                  # Calls list.Contains()
    print("Found")

# Strings (substring test)
if "ell" in "Hello":            # Calls str.Contains()
    print("Found substring")
```

## Needle Type

The needle (left operand) is checked against the container's element type as an argument into the
element slot. A constant converts when in range, a variable that C# cannot implicitly convert is
refused by name, and a non-numeric type mismatch is reported:

```python
xs: list[uint64] = [1, 2, 3]
print(2 in xs)            # True — constant 2 converts to uint64
# n: int32 = 2
# print(n in xs)          # SPY0222 — int32 cannot be used with uint64; cast
# print("a" in xs)        # SPY0222 — str is not a uint64
print(1 in [1, 2, 3])     # True  — same-width, always works
```

### Container kinds

`in` works on every container kind the iteration ring accepts, and the needle is checked against
that container's ELEMENT type in each case:

| container | needle slot | example |
|---|---|---|
| `list[T]`, `set[T]`, `frozenset[T]`, `array[T]` | `T` | `2 in [1, 2]` |
| `dict[K, V]` | `K` (keys, as in Python) | `"a" in {"a": 1}` |
| `str` | `str` (substring test) | `"a" in "ab"` |
| `bytes` | `int` or `bytes` | `97 in b"ab"`, `b"b" in b"ab"` |
| `tuple[...]` | the tuple's best common element type | `2 in (1, 2)` |
| `range(...)` | `int` | `3 in range(10)` |
| a class with `__contains__` (declared or inherited) | that method's parameter type | `2 in bag` |
| a CLR type with a public `Contains`, or any `IEnumerable[T]` | `T` | `2 in clr_list` |

A tuple is a container: `2 in (1, 2)` is `True`, and a heterogeneous tuple's element type is the
best common type of its elements, so `2 in (1, 2.5)` compares `2.0` against `1.0` and `2.5` and is
`False` — the same answer Python gives for a different reason (`2.0 != 2.5`).

Range membership is arithmetic, not a scan: `3 in range(10)` is answered in constant time from
`start`, `stop` and `step`, so it does not consume the range.

**Departure from Python (range needles).** `2.0 in range(5)` is `True` in Python; Sharpy refuses a
float needle against a range with SPY0222, because the needle slot is `int` (the same
`membership-needle-type-mismatch` departure catalogued below).


**Departure from Python.** Python's `in` never fails on a type mismatch — `"a" in [1, 2]` is simply
`False`. Sharpy refuses the needle at compile time (SPY0222): a value the element type cannot hold can
never be a member, so the test is a type error rather than a constant `False` (Axiom 2, type safety
over Python syntax). Catalogued as `membership-needle-type-mismatch` in `docs/deviations.yaml`.

### None needles

A needle whose own type is `T | None` (a name, an attribute, a call, a subscript) is accepted
wherever the `T` needle is: it is checked against the needle slot as `T`. A needle written as a
conditional expression or as a new `:=` binding is instead an argument into the slot, like the
argument of a call, and an optional branch or value is refused there; bind it to a
`T | None` name first. What the `None` of an accepted needle answers at run time is what Python
answers:

- in an equality scan (a list, set, dict, tuple or range), `None` equals no element, so `in` is
  `False` and `not in` is `True`;
- in `str` and `bytes`, whose `in` is a subsequence test, `None` raises `TypeError`;
- in a CLR-backed container, the receiver's own `Contains` answers: `False` for a `Counter` or an
  argparse `Namespace`, `AttributeError` for an IP network or an `EmailMessage`.

<!-- spec-sweep: output -->
```python
errno: int | None = None
print(errno in (111, 98))       # False
print(errno not in (111, 98))   # True
print(errno in range(200))      # False

code: int | None = 98
print(code in (111, 98))        # True

name: str | None = None
print(name in ["a", "b"])       # False
```

<!-- spec-sweep: output -->
```python
from collections import Counter
from ipaddress import IPv4Address, IPv4Network

name: str | None = None
try:
    print(name in "abc")
except TypeError:
    print("TypeError")

words: list[str] = ["a", "b"]
counts: Counter[str] = Counter(words)
print(name in counts)

address: IPv4Address | None = None
try:
    print(address in IPv4Network("10.0.0.0/8"))
except AttributeError:
    print("AttributeError")
```

```output
TypeError
False
AttributeError
```

The literal `None` is refused (SPY0222) unless the container's element type is itself optional:
written out, it is a needle the slot cannot hold, the same `membership-needle-type-mismatch`
departure. Into an optional element type it is a plain needle, and `None` can be a member:

<!-- spec-sweep: error SPY0222 -->
```python
xs: list[int] = [1, 2]
print(None in xs)               # SPY0222 — the slot is int, not int | None
```

<!-- spec-sweep: output -->
```python
ys: list[int | None] = [1, None]
print(None in ys)               # True
y: int | None = None
print(y in ys)                  # True
print(y in [1, 2])              # False
```

*Implementation*
- *Lowered: `x in y` emits `y.Contains(x)`. For Sharpy classes, `__contains__` compiles to a C# `Contains` method, so the same `.Contains()` call dispatches correctly for both Sharpy and .NET types.*
