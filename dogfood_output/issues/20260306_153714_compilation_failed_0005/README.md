# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T15:33:59.282149
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module features

from utils_core import Rectangle, Shape, ISizable
from utils_types import Point2D, Status
from utils_ops import process_shape, calculate_total

def main():
    # Create a point and check its distance from origin
    p: Point2D = Point2D(3.0, 4.0)
    print(p.distance_from_origin())

    # Create rectangles with different dimensions and positions
    r1: Rectangle = Rectangle(5.0, 10.0, Point2D(0.0, 0.0))
    r2: Rectangle = Rectangle(3.0, 4.0, p)

    # Test polymorphic dispatch through Shape type
    s: Shape = r1
    print(s.describe())

    # Process shapes showing status
    print(process_shape(r1))

    # Calculate individual areas
    print(r1.area())
    print(r2.get_size())

    # Calculate total area of all rectangles
    shapes: list[Rectangle] = [r1, r2]
    print(calculate_total(shapes))

    # Iterate over enum values
    for st in Status:
        print(st.name)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'UtilsTypes.Status' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'UtilsTypes.Status' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp270mb67_/utils_ops.spy:7:37
    |
  7 | def main():
    |            ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Status' is never used
  --> /tmp/tmp270mb67_/utils_ops.spy:3:41
    |
  3 | from utils_core import Rectangle, Shape, ISizable
    |                                         ^^^^^^
    |

warning[SPY0452]: Imported name 'ISizable' is never used
  --> /tmp/tmp270mb67_/main.spy:3:42
    |
  3 | from utils_core import Rectangle, Shape, ISizable
    |                                          ^^^^^^^^
    |


```

## Timing

- Generation: 177.64s
- Execution: 4.37s
