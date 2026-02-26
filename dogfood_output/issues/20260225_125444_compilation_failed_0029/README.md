# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T12:50:32.860777
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module imports

from geometry import IShape, Shape
from shapes import Rectangle, Circle, total_area, describe_all

def main():
    # Create some shapes
    rect: Rectangle = Rectangle(0.0, 0.0, 5.0, 3.0)
    circle: Circle = Circle(10.0, 10.0, 2.0)

    # Test individual shape methods
    print(rect.describe())
    print(rect.area())

    # Test circle area
    print(circle.area())

    # Test interface-based polymorphism through total_area function
    # Note: Generic collections are INVARIANT, so we create list[IShape]
    # and add items individually (cannot assign list[Shape] to list[IShape])
    shapes: list[IShape] = []
    shapes.append(rect)
    shapes.append(circle)

    total: float = total_area(shapes)
    print(total)

    # EXPECTED OUTPUT:
    # Shape at (0.0, 0.0) - Rectangle 5.0 x 3.0
    # 15.0
    # 12.56636
    # 27.56636
```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 1: cannot convert from 'Shapes.Rectangle' to 'Geometry.IShape'
  --> /tmp/tmphlc23gfw/main.spy:22:23
    |
 22 |     shapes.append(rect)
    |                       ^
    |

error[CS1503]: Argument 1: cannot convert from 'Shapes.Circle' to 'Geometry.IShape'
  --> /tmp/tmphlc23gfw/main.spy:23:23
    |
 23 |     shapes.append(circle)
    |                       ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmphlc23gfw/main.spy:3:30
    |
  3 | from geometry import IShape, Shape
    |                              ^^^^^
    |

warning[SPY0452]: Imported name 'describe_all' is never used
  --> /tmp/tmphlc23gfw/main.spy:4:51
    |
  4 | from shapes import Rectangle, Circle, total_area, describe_all
    |                                                   ^^^^^^^^^^^^
    |


```

## Timing

- Generation: 226.53s
- Execution: 4.26s
