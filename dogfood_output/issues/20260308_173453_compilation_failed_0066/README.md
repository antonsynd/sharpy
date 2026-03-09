# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T17:23:13.245715
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module usage
from shapes import Color
from geometry import Circle, Rectangle, Point2D
from utils import calculate_total_area, color_name, midpoint

def main():
    origin: Point2D = Point2D(0.0, 0.0)
    corner: Point2D = Point2D(6.0, 8.0)

    # Test Point2D.distance_to
    distance: float = origin.distance_to(corner)
    print(distance)

    # Test midpoint
    mid: Point2D = midpoint(origin, corner)
    print(mid.x)
    print(mid.y)

    # Create shapes
    c: Circle = Circle("Sun", Point2D(5.0, 5.0), 3.0, Color.RED)
    r: Rectangle = Rectangle("Door", Point2D(1.0, 1.0), 4.0, 6.0, Color.BLUE)

    # Build shape list with concrete type
    shapes: list[Shape] = []
    shapes.append(c)
    shapes.append(r)

    # Calculate total area
    total: float = calculate_total_area(shapes)
    print(total)

    # Print descriptions using concrete types
    print(c.describe())
    print(r.describe())

    # Access colors via method
    print(color_name(c.get_color()))
    print(color_name(r.get_color()))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Geometry.Shape' does not contain a definition for 'Area' and no accessible extension method 'Area' accepting a first argument of type 'Geometry.Shape' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpfy9pxcj4/utils.spy:8:31
    |
  8 |     corner: Point2D = Point2D(6.0, 8.0)
    |                               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Circle' is never used
  --> /tmp/tmpfy9pxcj4/utils.spy:3:26
    |
  3 | from geometry import Circle, Rectangle, Point2D
    |                          ^^^^^^
    |

warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpfy9pxcj4/utils.spy:3:34
    |
  3 | from geometry import Circle, Rectangle, Point2D
    |                                  ^^^^^^^^^
    |


```

## Timing

- Generation: 653.34s
- Execution: 4.85s
