# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T19:34:53.489120
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class usage
from shapes import Shape, Rectangle, Circle, IDrawable, Color
from utils import Point, ShapeUtils, create_default_rectangle

def process_shape(s: Shape) -> str:
    return f"Area: {s.area()}, Description: {s.describe()}"

def main():
    # Create points using struct from utils
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)

    # Calculate distance between points
    dist: float = ShapeUtils.distance(p1, p2)
    print(f"Distance: {dist}")

    # Create shapes from shapes module
    rect: Rectangle = Rectangle(5.0, 3.0, Color.RED)
    circle: Circle = Circle(2.5, Color.GREEN)

    # Test IDrawable interface
    drawable1: IDrawable = rect
    drawable2: IDrawable = circle
    print(drawable1.draw())
    print(drawable2.draw())

    # Test polymorphic area calculation
    shapes: list[Shape] = [rect, circle]
    total: float = 0.0
    for s in shapes:
        total += s.area()
    print(f"Total area: {total}")

    # Test enum iteration and access
    for c in Color:
        print(f"Color {c.name} = {c.value}")

    # Use factory function from utils
    default_rect: Rectangle = create_default_rectangle()
    print(f"Default rect area: {default_rect.area()}")

    # Test constructor chaining and cross-module inheritance
    print(rect.describe())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp42pzd64p/shapes.spy:24:80
    |
 24 |     print(drawable1.draw())
    |                            ^
    |


```

## Timing

- Generation: 166.72s
- Execution: 5.17s
