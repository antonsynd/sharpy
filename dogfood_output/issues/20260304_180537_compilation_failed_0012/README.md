# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T18:02:41.517853
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - orchestrates all modules
from geometry import Point, Rectangle, Circle
from geometry import FillStyle
from graphics import ShapeGroup, Renderer, create_sample_shapes

def analyze_shapes(shapes: list[Shape]) -> None:
    total: float = 0.0
    for s in shapes:
        total = total + s.area()
    print(total)

def main():
    # Test 1: Create and use Point struct
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = p1.distance_to(p2)
    print(dist)

    # Test 2: Create rectangles with different fill styles
    rect: Rectangle = Rectangle(10.0, 5.0, 0.0, 0.0)
    rect.fill = FillStyle.SOLID
    print(rect.area())
    print(rect.perimeter())
    print(rect.describe())

    # Test 3: Create circle
    circ: Circle = Circle(3.0, 3.0, 2.0)
    print(circ.area())
    print(circ.draw())

    # Test 4: Use ShapeGroup from graphics module
    shapes: ShapeGroup = ShapeGroup()
    r1: Rectangle = Rectangle(5.0, 2.0, 0.0, 0.0)
    c1: Circle = Circle(1.0, 1.0, 1.0)
    shapes.add(r1)
    shapes.add(c1)
    print(shapes.total_area())
    print(shapes.total_perimeter())

    # Test 5: Use Renderer static methods
    drawing: str = Renderer.render_drawing(rect)
    print(drawing)
    print(Renderer.render_drawing(circ))

    # Test 6: Test scaling via interface
    original_width: float = rect.width
    Renderer.scale_shape(rect, 2.0)
    print(rect.width)
    print(rect.area())

    # Test 7: Test create_sample_shapes
    sample_r1: Rectangle
    sample_c: Circle
    sample_r2: Rectangle
    sample_r1, sample_c, sample_r2 = create_sample_shapes()
    print(sample_r1.area())
    print(sample_c.perimeter())
    print(sample_r2.fill.name)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.FillStyle' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Shapes.FillStyle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp4s9ye9rp/main.spy:58:55
    |
 58 |     print(sample_r2.fill.name)
    |                               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmp4s9ye9rp/graphics.spy:4:15
    |
  4 | from graphics import ShapeGroup, Renderer, create_sample_shapes
    |               ^^^^^
    |

warning[SPY0451]: Local variable 'original_width' is assigned but never used
  --> /tmp/tmp4s9ye9rp/main.spy:46:5
    |
 46 |     original_width: float = rect.width
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 157.62s
- Execution: 4.81s
