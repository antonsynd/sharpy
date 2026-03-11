# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T17:55:58.424516
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point
from geometry import Shape, Circle, Rectangle, Point, Color, calculate_total, create_origin, color_name
from utils import format_point, greet

def main():
    # Test struct creation and method
    origin: Point = create_origin()
    corner: Point = Point(3.0, 4.0)
    
    print(origin.distance_to_origin())
    print(corner.distance_to_origin())
    
    # Test utils
    print(format_point(corner))
    print(greet("Sharpy"))
    
    # Test shapes with polymorphism
    c: Circle = Circle("Sun", 5.0)
    r: Rectangle = Rectangle("Box", 4.0, 3.0)
    
    # Test polymorphism via base class
    shapes: list[Shape] = [c, r]
    print(calculate_total(shapes))
    
    # Test method dispatch
    s1: Shape = c
    print(s1.draw())
    
    s2: Shape = r
    print(s2.draw())
    
    # Test description and area
    print(c.get_description())
    print(r.get_description())
    print(c.calculate_area())
    print(r.calculate_area())
    
    # Test enum usage
    primary: Color = Color.BLUE
    print(color_name(primary))
    
    # Test virtual method dispatch
    print(c.scale(2.0))
    print(r.scale(1.5))

```

## Error

```
Assembly compilation failed:

error[CS0506]: 'Geometry.Circle.GetDescription()': cannot override inherited member 'Geometry.Shape.GetDescription()' because it is not marked virtual, abstract, or override
  --> /tmp/tmpkef78aya/geometry.spy:37:32
    |
 37 |     
    |     ^
    |

error[CS0506]: 'Geometry.Rectangle.GetDescription()': cannot override inherited member 'Geometry.Shape.GetDescription()' because it is not marked virtual, abstract, or override
  --> /tmp/tmpkef78aya/geometry.spy:62:32


```

## Compiler Output

```
warning[SPY0452]: Imported name 'create_origin' is never used
  --> /tmp/tmpkef78aya/utils.spy:2:23
    |
  2 | from geometry import Shape, Circle, Rectangle, Point, Color, calculate_total, create_origin, color_name
    |                       ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 806.80s
- Execution: 5.10s
