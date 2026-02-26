# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T05:43:53.458121
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module integration

from types import Point, ShapeType, IHasArea, IDrawable
from shapes import Circle, Rectangle, Shape
from utils import Bounds, calculate_total_area, format_point, create_circle_at_origin

def main():
    # Create points and shapes
    p1: Point = Point(1.0, 2.0)
    p2: Point = Point(4.0, 6.0)
    circle: Circle = create_circle_at_origin(5.0)
    rect_center: Point = Point(10.0, 10.0)
    rect: Rectangle = Rectangle(rect_center, 3.0, 4.0)

    # Test struct methods and properties
    distance: float = p1.distance_to(p2)
    print(distance)

    # Test enum
    print(circle.shape_type.name)

    # Test interface polymorphism
    shapes: list[Shape] = [circle, rect]
    total_area: float = calculate_total_area(shapes)
    print(total_area)

    # Test interface method dispatch
    draw1: str = circle.draw()
    draw2: str = rect.draw()
    print(draw1)
    print(draw2)

    # Test inheritance
    desc1: str = circle.get_description()
    desc2: str = rect.get_description()
    print(desc1)
    print(desc2)

    # Test utility functions
    formatted: str = format_point(p1)
    print(formatted)

    # Test Bounds struct
    bounds: Bounds = Bounds(0.0, 0.0, 100.0, 100.0)
    print(bounds.get_width())

# EXPECTED OUTPUT:
# 5.0
# CIRCLE
# 90.53975
# Drawing circle at (0.0, 0.0)
# Drawing rectangle at (10.0, 10.0)
# Circle with radius 5.0
# Rectangle 3.0x4.0
# (1.0, 2.0)
# 100.0
```

## Error

```
Assembly compilation failed:

error[CS1729]: 'Shapes.Shape' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpq6w5wx2g/shapes.spy:58:77

error[CS1729]: 'Shapes.Shape' does not contain a constructor that takes 2 arguments
  --> /tmp/tmpq6w5wx2g/shapes.spy:37:60
    |
 37 |     print(desc2)
    |                 ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Rectangle' is never used
  --> /tmp/tmpq6w5wx2g/utils.spy:3:40
    |
  3 | from types import Point, ShapeType, IHasArea, IDrawable
    |                                        ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeType' is never used
  --> /tmp/tmpq6w5wx2g/main.spy:3:26
    |
  3 | from types import Point, ShapeType, IHasArea, IDrawable
    |                          ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IHasArea' is never used
  --> /tmp/tmpq6w5wx2g/main.spy:3:37
    |
  3 | from types import Point, ShapeType, IHasArea, IDrawable
    |                                     ^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmpq6w5wx2g/main.spy:3:47
    |
  3 | from types import Point, ShapeType, IHasArea, IDrawable
    |                                               ^^^^^^^^^
    |


```

## Timing

- Generation: 458.66s
- Execution: 4.35s
