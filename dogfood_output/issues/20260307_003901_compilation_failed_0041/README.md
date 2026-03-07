# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T00:34:49.401475
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module features
from config import Color, PI, TOLERANCE, get_color_name
from primitives import Point, IShape
from shapes import Circle, Rectangle, calculate_total_area

def main():
    # Create shapes with different colors and positions
    c1: Circle = Circle(5.0, Color.RED, Point(0.0, 0.0))
    r1: Rectangle = Rectangle(10.0, 4.0, Color.BLUE, Point(5.0, 5.0))
    r2: Rectangle = Rectangle(3.0, 3.0, Color.GREEN, Point(2.0, 2.0))

    # Test 1: Print descriptions
    print(c1.describe())
    print(r1.describe())

    # Test 2: Calculate and print areas
    print(c1.area())
    print(r1.area())

    # Test 3: Test polymorphic dispatch through list of ShapeBase
    # Using ShapeBase as the list type to avoid invariance issues
    shapes: list[ShapeBase] = []
    # Need to import ShapeBase for the list type
    shapes.append(c1)
    shapes.append(r1)
    shapes.append(r2)
    total: float = calculate_total_area(shapes)
    print(total)

    # Test 4: Color enum and name lookup
    print(get_color_name(c1.color))
    print(get_color_name(r2.color))

    # Test 5: Check square detection
    print(r2.is_square())

    # Test 6: Position string
    print(c1.get_position_str())

    # Test 7: Interface dispatch - cast to IShape for category
    shape1: IShape = c1
    shape2: IShape = r1
    print(shape1.get_category())
    print(shape2.get_category())

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Primitives.ShapeBase.Perimeter()' is abstract but it is contained in non-abstract type 'Primitives.ShapeBase'
  --> /tmp/tmpqe5uy0k0/primitives.spy:39:32
    |
 39 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'PI' is never used
  --> /tmp/tmpqe5uy0k0/primitives.spy:2:5
    |
  2 | from config import Color, PI, TOLERANCE, get_color_name
    |     ^^
    |

warning[SPY0452]: Imported name 'get_color_name' is never used
  --> /tmp/tmpqe5uy0k0/shapes.spy:2:9
    |
  2 | from config import Color, PI, TOLERANCE, get_color_name
    |         ^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'PI' is never used
  --> /tmp/tmpqe5uy0k0/main.spy:2:27
    |
  2 | from config import Color, PI, TOLERANCE, get_color_name
    |                           ^^
    |

warning[SPY0452]: Imported name 'TOLERANCE' is never used
  --> /tmp/tmpqe5uy0k0/main.spy:2:31
    |
  2 | from config import Color, PI, TOLERANCE, get_color_name
    |                               ^^^^^^^^^
    |


```

## Timing

- Generation: 221.50s
- Execution: 4.48s
