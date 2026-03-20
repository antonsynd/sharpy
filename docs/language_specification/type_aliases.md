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

Type aliases with function types are the preferred way to name callable signatures for internal use. For cases requiring variance annotations, event handler types, or a distinct named C# type, use a [delegate](delegates.md) instead. See [Delegates — When to use delegates](delegates.md#when-to-use-delegates) and [Function Types — Delegates vs function types](function_types.md#delegates-vs-function-types).

*Implementation*
- *🔄 Lowered - Inline expansion at use sites; `using` directive where possible.*
