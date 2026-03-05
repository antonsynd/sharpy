# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T13:48:47.313854
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class usage

from shapes import Drawable, Shape, Circle, Rectangle
from colors import Color, describe_color, get_primary_colors, color_count
from utils import Point, distance, format_area

def process_shape(s: Drawable) -> str:
    return s.draw()

def main():
    # Create shapes
    c: Circle = Circle(5.0)
    r: Rectangle = Rectangle(4.0, 6.0)
    
    # Print shape areas
    print(format_area(c.area()))
    print(format_area(r.area()))
    
    # Print shape descriptions via interface
    print(process_shape(c))
    print(process_shape(r))
    
    # Test inheritance - describe() from base Shape
    print(c.describe())
    
    # Check if rectangle is square
    print(r.is_square())
    
    # Create points and calculate distance
    p1: Point = Point(3.0, 4.0)
    p2: Point = Point(6.0, 8.0)
    
    # Distance from origin
    print(round(p1.distance_to_origin(), 1))
    
    # Distance between points
    print(round(distance(p1, p2), 1))
    
    # Work with colors enum
    primary: list[Color] = get_primary_colors()
    print(color_count())
    print(describe_color(Color.GREEN))

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'Hex' does not exist in the current context
  --> /tmp/tmp18ej8op9/colors.spy:11:70
    |
 11 |     # Create shapes
    |                    ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'primary' is assigned but never used
  --> /tmp/tmp18ej8op9/main.spy:40:5
    |
 40 |     primary: list[Color] = get_primary_colors()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmp18ej8op9/main.spy:3:30
    |
  3 | from shapes import Drawable, Shape, Circle, Rectangle
    |                              ^^^^^
    |


```

## Timing

- Generation: 85.93s
- Execution: 4.75s
