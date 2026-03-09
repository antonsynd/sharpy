# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T21:23:36.985944
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module features
from shapes import IShape, IDrawable, ShapeBase
from geometry_types import Point, Color, LineStyle
from shape_impl import Rectangle, Circle, create_default_rect, create_unit_circle

def process_shape(shape: IShape) -> str:
    area: float = shape.area()
    perim: float = shape.perimeter()
    return f"{shape.describe()} [area={area:.2f}, perim={perim:.2f}]"

def draw_if_possible(drawable: IDrawable) -> str:
    return drawable.draw()

def main():
    # Create points using the struct from geometry_types
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)

    # Test Point struct distance calculation
    dist: float = p1.distance_to(p2)
    print(f"Distance between points: {dist}")

    # Test enum values and names
    c: Color = Color.GREEN
    print(f"Selected color: {c.name}")
    print(f"Color value: {c.value}")

    # Create shapes using concrete implementations
    rect: Rectangle = Rectangle(5.0, 3.0, Color.RED, LineStyle.DASHED)
    circle: Circle = Circle(2.5, Point(1.0, 1.0))

    # Process shapes polymorphically using IShape interface
    print(process_shape(rect))
    print(process_shape(circle))

    # Draw shapes using IDrawable interface
    print(draw_if_possible(rect))
    print(draw_if_possible(circle))

    # Test factory functions from shape_impl
    default_rect: Rectangle = create_default_rect()
    unit_circle: Circle = create_unit_circle()
    print(f"Default rect area: {default_rect.area()}")
    print(f"Unit circle perimeter: {unit_circle.perimeter():.4f}")

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'GeometryTypes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'GeometryTypes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp21icildd/shape_impl.spy:29:194
    |
 29 |     rect: Rectangle = Rectangle(5.0, 3.0, Color.RED, LineStyle.DASHED)
    |                                                                       ^
    |

error[CS1061]: 'GeometryTypes.LineStyle' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'GeometryTypes.LineStyle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp21icildd/shape_impl.spy:33:91
    |
 33 |     print(process_shape(rect))
    |                               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'ShapeBase' is never used
  --> /tmp/tmp21icildd/main.spy:2:39
    |
  2 | from shapes import IShape, IDrawable, ShapeBase
    |                                       ^^^^^^^^^
    |


```

## Timing

- Generation: 534.53s
- Execution: 4.92s
