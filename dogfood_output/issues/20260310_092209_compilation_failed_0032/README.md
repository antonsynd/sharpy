# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T09:16:40.083931
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex cross-module imports
from types import Color, Point
from shapes import Circle, Rectangle, Shape
from utils import total_area, count_by_color, render_circle, render_rectangle, create_circle_at_origin

def main():
    # Create a point and shapes
    p: Point = Point(3.0, 4.0)
    c1: Circle = Circle(p, 5.0, Color.RED)
    c2: Circle = create_circle_at_origin(2.5, Color.BLUE)
    r: Rectangle = Rectangle(10.0, 8.0, Color.GREEN)

    # Test Point struct value type behavior
    print(p.distance_to_origin())

    # Calculate and print areas
    print(c1.area())
    print(r.area())

    # Test polymorphic collection
    shapes: list[Shape] = [c1, c2, r]
    print(total_area(shapes))

    # Count red shapes
    print(count_by_color(shapes, Color.RED))

    # Test shape rendering (using concrete types, not interface)
    print(render_circle(c1))
    print(render_rectangle(r))

    # Test polymorphic method dispatch
    print(c1.describe())
    print(r.describe())

    # Test enum properties
    print(c1.color.name)
    print(r.color.value)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Types.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Types.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3q8teax2/main.spy:36:48
    |
 36 |     print(c1.color.name)
    |                         ^
    |

error[CS1061]: 'Types.Color' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Types.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp3q8teax2/main.spy:37:47
    |
 37 |     print(r.color.value)
    |                         ^
    |


```

## Timing

- Generation: 295.37s
- Execution: 5.16s
