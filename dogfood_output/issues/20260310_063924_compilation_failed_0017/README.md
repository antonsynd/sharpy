# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T06:32:23.339009
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module imports and polymorphism
from shapes import Circle, Rectangle
from geometry import Shape
from utils import Color, Point

def main():
    # Create shapes
    circle = Circle("Circle", 5.0, Color.RED)
    rect = Rectangle("Rectangle", 4.0, 6.0, Color.BLUE)

    # Test polymorphism via @virtual methods
    print(circle.area())
    print(rect.area())

    # Test overridden describe method
    print(circle.describe())
    print(rect.describe())

    # Test color via polymorphic dispatch through base class
    print(rect.get_color())

    # Test struct value type
    p = Point(1.5, 2.5)
    print(p.x)

    # Test enum name access
    c = Color.GREEN
    print(c.name)

    # Test polymorphism using base class reference
    shape = circle
    print(shape.area())

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Utils.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmphh0h0pix/shapes.spy:47:66

error[CS1061]: 'Utils.Color' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Utils.Color' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmphh0h0pix/shapes.spy:24:66
    |
 24 |     print(p.x)
    |               ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmphh0h0pix/main.spy:3:22
    |
  3 | from geometry import Shape
    |                      ^^^^^
    |


```

## Timing

- Generation: 373.63s
- Execution: 5.11s
