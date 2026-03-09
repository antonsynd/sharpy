# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T14:24:09.082589
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point testing cross-module inheritance and polymorphism
from shapes import Shape, IDrawable, Color
from geometry import Rectangle, Circle, Point
from shape_utils import create_default_rect, create_default_circle, total_area, paint_description, get_shape_info

def main():
    # Create shapes using cross-module constructors
    rect: Rectangle = create_default_rect()
    circle: Circle = create_default_circle()

    # Test 1: Polymorphic method calls from base class
    shapes: list[Shape] = []
    shapes.append(rect)
    shapes.append(circle)
    print(total_area(shapes))

    # Test 2: Interface implementation across modules
    # Use interface type directly, not list covariance
    drawable1: IDrawable = rect
    drawable2: IDrawable = circle
    print(paint_description(drawable1))
    print(paint_description(drawable2))

    # Test 3: Virtual method dispatch across modules
    print(rect.describe())
    print(circle.describe())

    # Test 4: Enum access through cross-module inheritance
    print(rect.get_color_name())
    print(circle.get_color_name())

    # Test 5: Create and use shapes directly in main
    p1: Point = Point(2.0, 3.0)
    custom_rect: Rectangle = Rectangle(4.0, 6.0, p1, Color.YELLOW)
    print(get_shape_info(custom_rect))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmplm1jjwyk/shapes.spy:29:31
    |
 29 |     print(rect.get_color_name())
    |                               ^
    |


```

## Timing

- Generation: 338.85s
- Execution: 4.88s
