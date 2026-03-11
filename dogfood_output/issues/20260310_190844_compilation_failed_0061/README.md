# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T19:02:00.553361
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex multi-file imports and usage
from geometry_base import Shape, IDrawable, find_largest_shape
from shape_types import Color, Point, ShapeCategory, get_category_name, color_to_string
from concrete_shapes import Circle, Rectangle, Triangle

def main():
    # Create test points
    origin: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 0.0)
    p3: Point = Point(0.0, 4.0)

    # Create shapes from different modules
    circle: Circle = Circle("MyCircle", origin, 5.0, Color.RED)
    rect: Rectangle = Rectangle("MyRect", origin, 4.0, 6.0)
    tri: Triangle = Triangle("MyTri", origin, p2, p3)

    # Test 1: Virtual method dispatch
    print(circle.area())

    # Test 2: Interface implementation
    drawable: IDrawable = circle
    print(drawable.draw())

    # Test 3: Enum usage
    print(get_category_name(ShapeCategory.CIRCULAR))

    # Test 4: Color enum to string
    print(color_to_string(Color.BLUE))

    # Test 5: Shape collection and polymorphism
    shapes: list[Shape] = [circle, rect, tri]
    total: float = 0.0
    for s in shapes:
        total += s.area()
    print(total)

    # Test 6: Find largest shape
    largest: Shape = find_largest_shape(shapes)
    print(largest.name)

    # Test 7: Rectangle area (interface implementation)
    print(rect.area())

    # Test 8: Triangle perimeter (3-4-5 triangle)
    print(tri.perimeter())

```

## Error

```
Assembly compilation failed:

error[CS0115]: 'ConcreteShapes.Circle.Scale(double)': no suitable method found to override
  --> /tmp/tmpgrpii0qi/concrete_shapes.spy:41:30
    |
 41 |     # Test 7: Rectangle area (interface implementation)
    |                              ^
    |


```

## Compiler Output

```
warning[SPY0458]: @virtual is redundant on '__str__' in 'Shape' — it always overrides Object.ToString(). The @virtual decorator will be ignored.
  --> /tmp/tmpgrpii0qi/geometry_base.spy:8:33
    |
  8 |     origin: Point = Point(0.0, 0.0)
    |                                 ^^^
    |


```

## Timing

- Generation: 355.94s
- Execution: 5.15s
