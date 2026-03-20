# Membership Operators

| Operator | Description |
|----------|-------------|
| `in` | Membership test |
| `not in` | Negated membership |

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

If the type does not support membership testing, the compiler reports `SPY0333` ("Type does not support membership testing (missing `__contains__` method)").

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

*Implementation*
- *Lowered: `x in y` emits `y.Contains(x)`. For Sharpy classes, `__contains__` compiles to a C# `Contains` method, so the same `.Contains()` call dispatches correctly for both Sharpy and .NET types.*
