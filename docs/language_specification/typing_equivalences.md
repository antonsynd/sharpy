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
| `TypeIs` | Conversion functions (`-> T \| None`) + `as?` cast | see [User-Defined Type Guards](#user-defined-type-guards) |
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

## User-Defined Type Guards

Python has two constructs for *reusable, named* type narrowing: `TypeGuard` (PEP 647) and its successor
`TypeIs` (PEP 742). A function annotated `-> TypeIs[T]` returns `bool` at runtime but carries narrowing
metadata: where the call appears in a conditional, the type checker narrows the argument to `T` in the
true branch and *excludes* `T` in the false branch. (`TypeGuard` narrowed only the true branch; `TypeIs`
narrows both — behavior described here from PEP 742/647.)

**Sharpy does not add a `TypeIs[T]` return annotation.** Instead it delivers the same value —
reusable, named narrowing — with **zero new type-system machinery**, using two features it already has.

### Primary: conversion functions returning `T | None`

Write a function that *returns the narrowed value or `None`* instead of a `bool` that *asserts* a type.
This is the "parse, don't validate" idiom, and it is exactly what .NET's `as` operator does:

```python
def as_circle(s: Shape) -> Circle | None:
    return s if isinstance(s, Circle) else None

c = as_circle(shape)
if c is not None:
    print(c.radius)        # c narrowed to Circle by the EXISTING `is not None` narrowing
```

The narrowing here is **not new** — it is the same `is not None` rule documented in
[Type Narrowing](type_narrowing.md) (`is not None` narrows `T | None`/`T?` to `T`). The conversion
function is just an ordinary function; no call-based narrowing mode is needed in the type checker.

Why this fits Sharpy better than `TypeIs` (Axiom-ordered):

- **Direct .NET mapping (Axiom 1).** `Circle | None` maps to C# `Circle c = shape as Circle;` — a
  nullable value, zero-cost. (Return `T?` instead of `T | None` when you want a Sharpy-native
  `Optional[T]`; both ride `is not None` narrowing — see [Nullable Types](nullable_types.md).)
- **It is sound; `TypeIs` is not (Axiom 3).** `TypeIs[T]` is *trusted, not verified* — the compiler
  believes the annotation even if the body's logic is wrong. A conversion function is **checked**: the
  compiler confirms the body really produces `T | None`. This matters most exactly where `TypeIs` is
  most dangerous — **collections**:

  ```python
  def to_str_list(items: list[object]) -> list[str] | None:
      out: list[str] = []
      for x in items:
          if not isinstance(x, str):
              return None
          out.append(x)
      return out
  ```

  A `TypeIs[list[str]]` guard would *reinterpret the same `list[object]`* as `list[str]` — unsound,
  because the original alias can still append a non-`str` afterward. The conversion **builds a
  correctly-typed value**, so it is safe. (This also matches the stdlib convention: return nullable for
  absence; the caller opts into `Optional` via `maybe`.)
- **No compiler work (Axiom 2 ergonomics for free).** It rides narrowing that already exists, so the
  pattern is available today.

### Concise single-type checks: the `as?` failable cast

The conversion form's only wart is hand-writing `s if isinstance(s, T) else None` for a plain
type test. The experimental `as?` operator closes that gap — a small, lexical operator (not a
type-system subsystem) that lowers to a checked cast and evaluates to `None` on failure:

```python
c = shape as? Circle          # Circle? — None if shape is not a Circle
```

`as?` is experimental behind the `failable_cast` feature flag (tracking issue
[#1029](https://github.com/antonsynd/sharpy/issues/1029)); see
[Type Casting — `as?` / `as!`](type_casting.md#experimental-as--as-failable-casts) for the full
semantics. It composes with `??` / `?.` and, like the conversion form, introduces a narrowed binding
rather than reinterpreting the original variable in place.

### Why `TypeIs[T]` itself is deferred

Returning a value (and rebinding) instead of asserting a type gives up three things `TypeIs` has —
and, importantly, these are also the cases where in-place narrowing becomes **unsound**:

1. **Negative-branch narrowing** — `TypeIs` makes the `else` branch exclude `T`; the conversion form
   does not.
2. **In-place narrowing without rebinding** — e.g. narrowing a field, or a variable across a `while`,
   without introducing a new binding.
3. **Correlated multi-argument guards** — "if `is_valid(a, b)` then narrow *both* `a` and `b`."

Cases 1–3 are precisely where in-place narrowing gets unsound (mutable aliases, fields mutated between
check and use), which is why C#'s own `is Circle c` pattern **re-binds** too. A trusted-not-verified
`TypeIs[T]` would reintroduce that unsoundness — worst of all for collections, as shown above.

`TypeIs[T]` is therefore **deferred (demand-gated)**: it is revisited *only if* the negative-branch /
in-place / correlated-multi-argument gap demonstrably bites in real code, and if adopted it is scoped to
*that gap* rather than as the primary way to define user narrowing. The conversion-function + `as?`
pattern covers ~90% of `TypeIs`'s value, fully soundly, with a direct CLR mapping and no new type-system
subsystem — consistent with the axiom precedence (.NET > Type Safety > Python Syntax) and the
"magic behavior" and "runtime type checking" anti-patterns Sharpy avoids.

## See Also

- [Type Narrowing](type_narrowing.md)
- [Type Casting](type_casting.md)
- [Nullable Types](nullable_types.md)
- [Type Annotations](type_annotations.md)
- [Generics](generics.md)
- [Dataclass](dataclass.md)
- [Interfaces](interfaces.md)
