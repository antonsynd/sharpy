# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T06:15:51.667087
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class inheritance
from shapes import Shape, Circle, Rectangle
from colored_shapes import ColoredCircle, Drawing

def main():
    # Create base shapes from shapes module
    circle = Circle(5.0)
    rect = Rectangle(3.0, 4.0)

    # Create colored shape from colored_shapes module
    colored_circle = ColoredCircle(2.0, "Red")

    # Test base class methods
    print(circle.area())
    print(rect.area())

    # Test polymorphism - colored circle IS a circle
    print(colored_circle.area())
    print(colored_circle.describe())

    # Test drawing with mixed shapes
    drawing = Drawing()
    drawing.add_shape(circle)
    drawing.add_shape(rect)
    drawing.add_shape(colored_circle)
    print(drawing.count_shapes())
    print(drawing.total_area())

    # Test method-based color access
    print(colored_circle.get_color())

# EXPECTED OUTPUT:
# 78.54
# 12.0
# 12.57
# Red Circle (r=2.0)
# 3
# 103.11
# Red
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'pi' does not exist in the current context
  --> /tmp/tmpb8aoaig_/shapes.spy:29:20
    |
 29 |     # Test method-based color access
    |                    ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpb8aoaig_/main.spy:2:20
    |
  2 | from shapes import Shape, Circle, Rectangle
    |                    ^^^^^
    |


```

## Timing

- Generation: 639.18s
- Execution: 4.23s
