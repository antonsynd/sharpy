# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T12:06:24.313371
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module functionality
from types import ShapeStatus, Point
from shapes import Shape, Rectangle, Circle, Triangle
from utils import create_rectangle, create_circle, measure_shape, scale_shape, check_status, total_area, process_shapes

def main():
    # Create shapes using factory functions from utils
    rect: Rectangle = create_rectangle(5.0, 3.0)
    circle: Circle = create_circle(2.0)
    
    # Create triangle directly
    t1: Point = Point(0.0, 0.0)
    t2: Point = Point(4.0, 0.0)
    t3: Point = Point(0.0, 3.0)
    tri: Triangle = Triangle(t1, t2, t3)
    
    # Measure shapes using IMeasurable interface
    rect_measure: tuple[float, float] = measure_shape(rect)
    print(rect_measure[0])
    print(rect_measure[1])
    
    # Scale using ITransformable interface
    scale_shape(rect, 2.0)
    print(rect.area())
    
    # Check status returns string
    status_result: str = check_status(circle)
    print(status_result)
    
    # Total area calculation
    shapes: list[IMeasurable] = [rect, circle, tri]
    print(total_area(shapes))
    
    # Process and print status
    all_shapes: list[Shape] = [rect, circle, tri]
    processed: list[str] = process_shapes(all_shapes)
    for p in processed:
        print(p)

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'Shapes.Rectangle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> /tmp/tmp5rn3cgtx/shapes.spy:19:18
    |
 19 |     print(rect_measure[0])
    |                  ^
    |

error[CS0534]: 'Shapes.Circle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> /tmp/tmp5rn3cgtx/shapes.spy:42:18

error[CS0534]: 'Shapes.Triangle' does not implement inherited abstract member 'Shapes.Shape.Perimeter()'
  --> /tmp/tmp5rn3cgtx/shapes.spy:66:18


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Triangle' is never used
  --> /tmp/tmp5rn3cgtx/utils.spy:4:19
    |
  4 | from utils import create_rectangle, create_circle, measure_shape, scale_shape, check_status, total_area, process_shapes
    |                   ^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeStatus' is never used
  --> /tmp/tmp5rn3cgtx/main.spy:2:19
    |
  2 | from types import ShapeStatus, Point
    |                   ^^^^^^^^^^^
    |


```

## Timing

- Generation: 339.01s
- Execution: 5.11s
