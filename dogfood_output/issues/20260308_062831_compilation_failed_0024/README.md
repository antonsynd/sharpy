# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T06:17:58.830316
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module imports
from geometry import Point, ShapeBase, IMeasurable, IDrawable
from shapes import Rectangle, Circle
from utils import create_default_rectangle, create_default_circle, process_shapes, total_area, Counter

# Custom square class defined in main
class Square(Rectangle):
    def __init__(self, side: float):
        super().__init__(side, side)

    @override
    def get_name(self) -> str:
        return "Square"

    @override
    def draw(self) -> str:
        return "Drawing square with side {}".format(self.width)

def main():
    # Reset counter at start
    Counter.reset()

    # Create shapes using factories
    rect: Rectangle = create_default_rectangle()
    circ: Circle = create_default_circle()
    
    # Create a square (subclass defined in main)
    square: Square = Square(4.0)

    # Test counter
    Counter.increment()
    Counter.increment()
    print(Counter.get_count())

    # Store shapes in list
    shapes: list[IMeasurable] = list()
    shapes.append(rect)
    shapes.append(circ)
    shapes.append(square)

    # Print shape names and measurements
    print(rect.get_name())
    print(rect.measure())
    print(circ.get_name())
    print(circ.measure())
    print(square.get_name())
    print(square.measure())

    # Test total area
    print(total_area(shapes))

    # Test drawing (interface dispatch)
    print(rect.draw())
    print(circ.draw())
    print(square.draw())

    # Test scaling (virtual method dispatch)
    rect.scale(2.0)
    print(rect.measure())
    circ.scale(3.0)
    print(circ.measure())

    # Test Point operations
    p1: Point = Point(3.0, 4.0)
    p2: Point = Point(0.0, 0.0)
    print(p1.distance_to(p2))

    # Test process_shapes with lambda
    areas: list[float] = process_shapes(shapes, lambda s: s.measure())
    print(len(areas))

```

## Error

```
Assembly compilation failed:

error[CS0115]: 'Geometry.Point.Measure()': no suitable method found to override
  --> /tmp/tmptfwqj_o5/geometry.spy:37:32
    |
 37 |     shapes.append(rect)
    |                        ^
    |

error[CS1503]: Argument 1: cannot convert from 'double' to 'string'
  --> /tmp/tmptfwqj_o5/shapes.spy:54:56
    |
 54 |     print(circ.draw())
    |                       ^
    |

error[CS1503]: Argument 1: cannot convert from 'double' to 'string'
  --> /tmp/tmptfwqj_o5/main.spy:17:57
    |
 17 |         return "Drawing square with side {}".format(self.width)
    |                                                         ^
    |

error[CS1503]: Argument 1: cannot convert from 'double' to 'string'
  --> /tmp/tmptfwqj_o5/shapes.spy:29:53
    |
 29 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IMeasurable' is never used
  --> /tmp/tmptfwqj_o5/shapes.spy:2:11
    |
  2 | from geometry import Point, ShapeBase, IMeasurable, IDrawable
    |           ^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'ShapeBase' is never used
  --> /tmp/tmptfwqj_o5/main.spy:2:29
    |
  2 | from geometry import Point, ShapeBase, IMeasurable, IDrawable
    |                             ^^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDrawable' is never used
  --> /tmp/tmptfwqj_o5/main.spy:2:53
    |
  2 | from geometry import Point, ShapeBase, IMeasurable, IDrawable
    |                                                     ^^^^^^^^^
    |


```

## Timing

- Generation: 599.38s
- Execution: 5.06s
