# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T03:37:11.658978
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module usage
# Type aliases MUST be at the top before use
from types import Color, Point
from shapes import Circle, Rectangle, Triangle
from utils import GeometryUtils, ShapeContainer

# Type alias for ShapeContainer usage
type ShapeContainerShape = ShapeContainer[Shape]

def main():
    # Create Point using struct constructor
    origin: Point = GeometryUtils.origin()
    p1: Point = Point(10.0, 20.0)
    p2: Point = Point(30.0, 40.0)

    # Test Point methods from types module
    dist: float = origin.distance_to(p1)
    print(dist)

    # Create shapes from shapes module with cross-module inheritance
    circle: Circle = Circle(origin, 5.0, Color.RED)
    rect: Rectangle = Rectangle(p1, 8.0, 6.0, Color.BLUE)
    tri: Triangle = Triangle(p2, 6.0, 4.0, Color.GREEN)

    # Draw shapes - virtual dispatch
    print(circle.draw())
    print(rect.draw())
    print(tri.draw())

    # Test GeometryUtils static methods from utils module
    ds: float = GeometryUtils.distance_squared(p1, p2)
    print(ds)

    # Test Color brightness
    brightness: str = GeometryUtils.color_brightness(Color.WHITE)
    print(brightness)

    # Use generic ShapeContainer with Shape constraint
    container: ShapeContainerShape = ShapeContainer[Shape](10)
    container.add(circle)
    container.add(rect)
    container.add(tri)

    # Calculate total area
    total: float = container.total_area()
    print(total)

    # Count shapes in container
    count: int = container.count()
    print(count)

```

## Error

```
Assembly compilation failed:

error[CS0238]: 'Shapes.Shape.PositionStr()' cannot be sealed because it is not an override
  --> /tmp/tmpbwax3pwc/shapes.spy:19:30
    |
 19 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpbwax3pwc/utils.spy:2:35
    |
  2 | # Type aliases MUST be at the top before use
    |                                   ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpbwax3pwc/utils.spy:3:21
    |
  3 | from types import Color, Point
    |                     ^^^^^
    |

warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpbwax3pwc/utils.spy:3:28
    |
  3 | from types import Color, Point
    |                            ^^^
    |

warning[SPY0452]: Imported name 'Triangle' is never used
  --> /tmp/tmpbwax3pwc/utils.spy:4:8
    |
  4 | from shapes import Circle, Rectangle, Triangle
    |        ^^^^^^^^
    |


```

## Timing

- Generation: 537.94s
- Execution: 4.60s
