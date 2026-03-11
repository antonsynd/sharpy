# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T06:42:19.528038
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class usage

from shapes import Shape, Rectangle, Circle, IDrawable, IMeasurable
from utils import Point, BorderStyle, Style
from factories import ShapeFactory, ShapeMetrics


def main():
    # Create shapes using factory
    rect: Rectangle = ShapeFactory.create_rectangle("Box", 5.0, 3.0)
    square: Rectangle = ShapeFactory.create_rectangle("Square", 4.0, 4.0)
    circle: Circle = ShapeFactory.create_circle("Disk", 2.5)
    
    # Create metrics collector
    metrics: ShapeMetrics = ShapeMetrics(0.0, 0.0)
    metrics.add_shape(rect)
    metrics.add_shape(square)
    metrics.add_shape(circle)
    
    # Polymorphic dispatch through base class
    shapes: list[Shape] = [rect, square, circle]
    
    # Print 1: Polymorphic area calculation
    total_area: float = 0.0
    for s in shapes:
        total_area += s.area()
    print(total_area)
    
    # Print 2: Polymorphic draw calls
    for s in shapes:
        print(s.draw())
    
    # Print 3: Metrics total
    print(metrics.measure())
    
    # Print 4: Test interface implementation
    drawable: IDrawable = rect
    print(drawable.draw())
    
    # Print 5: Check if square using Rectangle method
    print(square.is_square())
    print(rect.is_square())
    
    # Print 6-8: Enum and struct usage
    styles: list[BorderStyle] = metrics.count_by_style()
    for st in styles:
        print(st.name)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Utils.BorderStyle' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.BorderStyle' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpipbnp5jt/utils.spy:30:49
    |
 30 |     for s in shapes:
    |                     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmpipbnp5jt/main.spy:3:57
    |
  3 | from shapes import Shape, Rectangle, Circle, IDrawable, IMeasurable
    |                                                         ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Point' is never used
  --> /tmp/tmpipbnp5jt/main.spy:4:19
    |
  4 | from utils import Point, BorderStyle, Style
    |                   ^^^^^
    |

warning[SPY0452]: Imported name 'Style' is never used
  --> /tmp/tmpipbnp5jt/main.spy:4:39
    |
  4 | from utils import Point, BorderStyle, Style
    |                                       ^^^^^
    |


```

## Timing

- Generation: 137.64s
- Execution: 4.96s
