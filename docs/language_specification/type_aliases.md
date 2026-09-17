# Type Aliases

Type aliases create readable names for complex types:

```python
# Module-level aliases
type UserId = int
type Coordinate = tuple[float, float]
type Matrix = list[list[float]]

# Generic aliases
type Callback[T] = (T) -> None
type Res[T, E] = Result[T, E]

# Class-level aliases
class Geometry:
    type Point3D = tuple[float, float, float]

    def distance(self, p1: Point3D, p2: Point3D) -> float:
        dx, dy, dz = p1[0] - p2[0], p1[1] - p2[1], p1[2] - p2[2]
        return (dx**2 + dy**2 + dz**2) ** 0.5

# Function-level aliases
def process_data[T, E](items: dict[str, list[Result[T, E]]]) -> dict[str, list[Result[T, E]]]:
    type DataMap = dict[str, list[Result[T, E]]]
    result: DataMap = {}
    # ...
    return result
```

## Class-level aliases

A `type` alias declared inside a type body is a nested member of that type (see
[Nested Types](nested_types.md)). Inside the host body the alias is referenced
bare, exactly as `Point3D` is used in `distance` above. From **outside** the host
it is referenced with the qualified spelling `Outer.Alias` — the same dot notation
used for any nested type — in addition to the bare-inside form:

```python
class Box:
    type Ints = list[int]

    def make(self) -> Box.Ints:      # qualified spelling, legal inside too
        xs: Box.Ints = [1, 2, 3]
        return xs


def main() -> None:
    b: Box = Box()
    result: Box.Ints = b.make()      # qualified from outside the host
    print(result)                    # [1, 2, 3]
```

A nested alias emits no runtime member of its own: like a module-level alias it is
lowered by inline expansion / a `using` alias, so `Box.Ints` is `list[int]` in
every position.

## Transparency

An alias is the target type in every position, including call position:

```python
type bint = int
v: bint = bint("42")  # compiles and runs identically to int("42")
```

This is a deliberate deviation from CPython 3.12, where `type bint = int; bint("42")` raises `TypeError` because `typing.TypeAliasType` is not callable. Sharpy's `type` aliases are compile-time transparent — the alias IS the target type, and every spelling the target accepts, the alias accepts identically. See `docs/deviations.yaml` for the formal record.

Module-qualified aliases are equally transparent: `lib.Handle` in value position resolves identically to a bare `Handle` imported from the same module.

Type aliases with function types are the preferred way to name callable signatures for internal use. For cases requiring variance annotations, event handler types, or a distinct named C# type, use a [delegate](delegates.md) instead. See [Delegates — When to use delegates](delegates.md#when-to-use-delegates) and [Function Types — Delegates vs function types](function_types.md#delegates-vs-function-types).

*Implementation*
- *🔄 Lowered - Inline expansion at use sites; `using` directive where possible.*
