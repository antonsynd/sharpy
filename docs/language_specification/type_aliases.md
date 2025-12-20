## Type Aliases **[v0.1.7]**

Type aliases create readable names for complex types:

```python
# Module-level aliases
type UserId = int
type Coordinate = tuple[double, double]
type Matrix = list[list[double]]

# Generic aliases
type Callback[T] = (T) -> None
type Res[T, E] = Result[T, E]

# Class-level aliases
class Geometry:
    type Point3D = tuple[double, double, double]

    def distance(self, p1: Point3D, p2: Point3D) -> double:
        dx, dy, dz = p1[0] - p2[0], p1[1] - p2[1], p1[2] - p2[2]
        return (dx**2 + dy**2 + dz**2) ** 0.5

# Function-level aliases
def process_data[T, E](items: dict[str, list[Result[T, E]]]) -> dict[str, list[Result[T, E]]]:
    type DataMap = dict[str, list[Result[T, E]]]
    result: DataMap = {}
    # ...
    return result
```

*Implementation: 🔄 Lowered - Inline expansion at use sites; `using` directive where possible.*

---

