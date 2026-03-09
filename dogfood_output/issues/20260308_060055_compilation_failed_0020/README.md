# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T05:57:01.719425
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# main.spy - Entry point demonstrating cross-module imports
from shapes import Color, IDrawable
from geometry import Rectangle, Circle, Point
from utils import total_area, create_origin

def main():
    # Create shapes with different colors and dimensions
    r1: Rectangle = Rectangle(3.0, 4.0, Color.RED)
    r2: Rectangle = Rectangle(5.0, 4.0, Color.BLUE)
    c1: Circle = Circle(3.0, Color.YELLOW)
    c2: Circle = Circle(2.0, Color.GREEN)

    # Test interface method
    d: IDrawable = r1
    print(d.draw())

    # Test virtual method from base class via concrete instances
    print(c1.describe())

    # Test struct
    origin: Point = create_origin()
    print(origin.x)

    # Test enum value access
    print(c2.color.value)

    # Calculate and print total area of all shapes
    shapes: list[Shape] = [r1, r2, c1, c2]
    total: float = total_area(shapes)
    print(total)

    # Test inheritance and polymorphism
    print(r1.area())
    print(c1.area())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Shapes.Color' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Shapes.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpolk_g0li/main.spy:25:48
    |
 25 |     print(c2.color.value)
    |                          ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Color' is never used
  --> /tmp/tmpolk_g0li/utils.spy:2:20
    |
  2 | from shapes import Color, IDrawable
    |                    ^^^^^
    |


```

## Timing

- Generation: 201.17s
- Execution: 4.82s
