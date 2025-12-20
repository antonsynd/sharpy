## Generics

### Generic Classes

```python
class Box[T]:
    """A container for a single value."""
    _value: T

    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value

    def set(self, value: T) -> None:
        self._value = value

# Usage
int_box = Box[int](42)
str_box = Box[str]("hello")
```

*Implementation: ✅ Native - `class Box<T>`*

### Generic Functions

```python
def identity[T](value: T) -> T:
    return value

def first[T](items: list[T]) -> T:
    return items[0]
```

*Implementation: ✅ Native - `T Identity<T>(T value)`*

### Type Constraints

```python
interface IComparable[T]:
    def __lt__(self, other: T) -> bool: ...

def find_max[T: IComparable[T]](items: list[T]) -> T:
    """Find the maximum item (must be comparable)."""
    max_item = items[0]
    for item in items:
        if max_item < item:
            max_item = item
    return max_item
```

| Constraint | C# Equivalent |
|------------|---------------|
| `T: Interface` | `where T : Interface` |
| `T: class` | `where T : class` |
| `T: struct` | `where T : struct` |

*Implementation: ✅ Native - Direct mapping to C# generic constraints.*

---
