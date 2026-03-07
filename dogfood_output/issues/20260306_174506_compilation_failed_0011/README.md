# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T17:39:17.437819
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point
from geometry import ShapeColor, Point, Rectangle
from shapes import Circle, Triangle
from utils import create_random_color, ShapeStatistics, process_drawables

def main():
    # Create a colored circle
    center: Point = Point(5.0, 3.0)
    circle: Circle = Circle(center, 2.5, ShapeColor.RED)

    print(circle.color_name())
    print(circle.area())
    print(circle.describe())

    # Create a triangle
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(4.0, 0.0)
    p3: Point = Point(2.0, 3.0)
    tri: Triangle = Triangle(p1, p2, p3, ShapeColor.BLUE)
    print(tri.describe())
    print(tri.area())

    # Use utils module
    ShapeStatistics.increment_count()
    ShapeStatistics.increment_count()
    print(ShapeStatistics.get_count())

    # Create drawables and process them
    d1: Circle = Circle(Point(0.0, 0.0), 1.0, create_random_color(0))
    d2: Circle = Circle(Point(1.0, 1.0), 2.0, create_random_color(1))
    drawables: list[Circle] = [d1, d2]

    descriptions: list[str] = process_drawables(drawables)
    for desc in descriptions:
        print(desc)

    # Use struct from geometry module
    rect: Rectangle = Rectangle(5.0, 10.0)
    print(rect.area())

    # Scale a circle and check area
    big_circle: Circle = circle.scale(2.0)
    print(big_circle.area())

```

## Error

```
Assembly compilation failed:

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpeixpvobu/utils.spy:19:10
    |
 19 |     tri: Triangle = Triangle(p1, p2, p3, ShapeColor.BLUE)
    |          ^
    |

error[CS0246]: The type or namespace name 'StaticmethodAttribute' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpeixpvobu/utils.spy:23:10
    |
 23 |     # Use utils module
    |          ^
    |

error[CS0103]: The name 'vertices' does not exist in the current context
  --> /tmp/tmpeixpvobu/shapes.spy:66:123

error[CS1503]: Argument 1: cannot convert from 'Sharpy.List<Shapes.Circle>' to 'Sharpy.List<Geometry.IDrawable>'
  --> /tmp/tmpeixpvobu/main.spy:33:61
    |
 33 |     descriptions: list[str] = process_drawables(drawables)
    |                                                           ^
    |

error[CS1061]: 'Geometry.ShapeColor' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Geometry.ShapeColor' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpeixpvobu/geometry.spy:25:32
    |
 25 |     ShapeStatistics.increment_count()
    |                                ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpeixpvobu/utils.spy:3:3
    |
  3 | from shapes import Circle, Triangle
    |   ^^^^^
    |


```

## Timing

- Generation: 318.84s
- Execution: 4.95s
