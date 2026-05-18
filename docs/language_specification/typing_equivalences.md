# Typing Module Equivalences

Sharpy has native type syntax that replaces the Python `typing` module. There is no `typing` module in Sharpy -- importing it will produce a compiler error directing you to the equivalent native syntax.

This document maps Python `typing` constructs to their Sharpy equivalents.

## Equivalence Table

| Python `typing` | Sharpy Native | Example |
|---|---|---|
| `Optional[X]` | `X?` | `x: int? = None` |
| `List[X]` | `list[X]` | `items: list[int] = [1, 2, 3]` |
| `Dict[K, V]` | `dict[K, V]` | `scores: dict[str, int] = {}` |
| `Set[X]` | `set[X]` | `tags: set[str] = set()` |
| `Tuple[X, Y]` | `tuple[X, Y]` | `point: tuple[int, int] = (1, 2)` |
| `Union[X, Y]` | `union` keyword (tagged) or `X?` (nullable) | `x: int? = None` |
| `Callable[[X], Y]` | `(X) -> Y` | `f: (int) -> str` |
| `Any` | Not supported -- use concrete types or generics | `def identity[T](x: T) -> T` |
| `TypeVar` | Generic type parameters `[T]` | `class Box[T]` |
| `Protocol` | `interface` keyword | `interface Printable` |
| `Final` | `@final` decorator | `@final def method()` |
| `Self` | `Self` type (no import needed) | `def clone(self) -> Self` |
| `TypeAlias` | `type X = Y` syntax | `type UserId = int` |
| `TypeGuard` | `x is T` narrowing | `if x is int:` |
| `NamedTuple` | `type X = tuple[name: type, ...]` | `type Point = tuple[x: float, y: float]` |
| `ClassVar` | Class-level field declarations | Direct class body fields |
| `Literal` | String literal types | Direct usage without import |

## Dataclasses Module

Sharpy has a native `@dataclass` decorator. No import is needed.

| Python `dataclasses` | Sharpy Native |
|---|---|
| `from dataclasses import dataclass` | Just use `@dataclass` directly |
| `field(default=...)` | Default values in class body: `x: int = 0` |
| `@dataclass(frozen=True)` | `@dataclass(frozen=True)` (same syntax) |
| `@dataclass(eq=True)` | `@dataclass(eq=True)` (same syntax) |

Example:

```python
# Python
from dataclasses import dataclass

@dataclass
class Point:
    x: float
    y: float
```

```python
# Sharpy (no import needed)
@dataclass
class Point:
    x: float
    y: float
```

## See Also

- [Type Annotations](type_annotations.md)
- [Generics](generics.md)
- [Dataclass](dataclass.md)
- [Interfaces](interfaces.md)
