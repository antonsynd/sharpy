# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T19:54:04.756242
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module usage
# Tests: Complex imports, interface dispatch, inheritance, generics

from geometric_base import Point, IGeometry
from shapes_impl import Shape, Rectangle, Circle, ShapeType
from shape_utils import total_area, filter_by_shape_type, scale_all, process_shapes_with_callback

def main():
    # Create shapes with Points from geometric_base
    rect: Rectangle = Rectangle(1, Point(0.0, 0.0), 10.0, 5.0)
    circle: Circle = Circle(2, Point(5.0, 5.0), 3.0)

    # Test: Method access across module
    print(rect.get_area())

    # Test: Virtual method dispatch across module
    print(rect.describe())
    print(circle.describe())

    # Test: Enum usage across module
    shapes: list[Shape] = []
    shapes.append(rect)
    shapes.append(circle)
    circles_only: list[Shape] = filter_by_shape_type(shapes, ShapeType.CIRCLE)
    print(len(circles_only))

    # Test: Tuple return from cross-module method
    bounds: tuple[float, float] = circle.get_bounds()
    print(bounds[0] + bounds[1])

    # Test: Higher-order function from cross-module
    descriptions: list[str] = process_shapes_with_callback(shapes, lambda s: s.describe())
    print(len(descriptions))

    # Test: Interface dispatch with total_area
    geometries: list[IGeometry] = []
    geometries.append(rect)
    geometries.append(circle)
    area_sum: float = total_area(geometries)
    print(area_sum)

    # Test: Mutation via cross-module method
    scale_all(shapes, 2.0)
    print(rect.get_area())
    print(circle.get_area())

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'ShapesImpl.Rectangle' does not implement inherited abstract member 'ShapesImpl.Shape.GetArea()'
  --> /tmp/tmp67zgw9zn/shapes_impl.spy:26:18
    |
 26 | 
    | ^
    |

error[CS0534]: 'ShapesImpl.Circle' does not implement inherited abstract member 'ShapesImpl.Shape.GetArea()'
  --> /tmp/tmp67zgw9zn/shapes_impl.spy:42:18
    |
 42 |     # Test: Mutation via cross-module method
    |                  ^
    |


```

## Timing

- Generation: 340.38s
- Execution: 4.88s
