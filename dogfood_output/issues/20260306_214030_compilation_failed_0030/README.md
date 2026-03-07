# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T21:36:56.855965
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module usage
from definitions import IShape, ITransformable, ShapeType, Point
from shapes import Rectangle, Circle
from utils import Pair, process_shape, create_pair

def main():
    # Create shapes using cross-module classes
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(5.0)

    # Test 1: Access interface methods from cross-module inheritance
    print(rect.area())

    # Test 2: Second shape's area
    print(circle.area())

    # Test 3: Test struct value semantics across modules
    p1: Point = Point(0.0, 0.0)
    p2: Point = Point(3.0, 4.0)
    dist: float = p1.distance_to(p2)
    print(dist)

    # Test 4: Enum value access
    rect_type: ShapeType = rect.get_type()
    print(rect_type.value)

    # Test 5: Interface function with cross-module type
    result: float = process_shape(circle)
    print(result)

    # Test 6: Interface method from ITransformable across modules
    bounds: tuple[float, float] = rect.get_bounds()
    print(bounds[0])

    # Test 7: Combined operations
    combined: float = rect.area() + circle.perimeter()
    print(combined)

```

## Error

```
Assembly compilation failed:

error[CS0266]: Cannot implicitly convert type 'object' to 'Definitions.Point'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmpif1dwwms/definitions.spy:33:35
    |
 33 |     print(bounds[0])
    |                     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'IShape' is never used
  --> /tmp/tmpif1dwwms/main.spy:2:25
    |
  2 | from definitions import IShape, ITransformable, ShapeType, Point
    |                         ^^^^^^
    |

warning[SPY0452]: Imported name 'ITransformable' is never used
  --> /tmp/tmpif1dwwms/main.spy:2:33
    |
  2 | from definitions import IShape, ITransformable, ShapeType, Point
    |                                 ^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Pair' is never used
  --> /tmp/tmpif1dwwms/main.spy:4:19
    |
  4 | from utils import Pair, process_shape, create_pair
    |                   ^^^^
    |

warning[SPY0452]: Imported name 'create_pair' is never used
  --> /tmp/tmpif1dwwms/main.spy:4:40
    |
  4 | from utils import Pair, process_shape, create_pair
    |                                        ^^^^^^^^^^^
    |


```

## Timing

- Generation: 176.74s
- Execution: 5.72s
