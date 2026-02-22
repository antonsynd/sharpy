# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T05:28:17.545861
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - tests cross-module inheritance with abstract methods

from base_shapes import Shape
from shapes_impl import Circle, Rectangle

def print_shape_info(s: Shape, name: str) -> None:
    print(f"{name} area: {s.area():.2f}")
    print(f"{name} perimeter: {s.perimeter():.2f}")

def main():
    c: Circle = Circle(5.0)
    r: Rectangle = Rectangle(4.0, 6.0)
    
    print_shape_info(c, "Circle")
    print_shape_info(r, "Rectangle")
    print(f"Circle radius: {c.radius}")
    print(f"Rectangle dimensions: {r.width} x {r.height}")
    # EXPECTED OUTPUT:
    # Circle area: 78.54
    # Circle perimeter: 31.42
    # Rectangle area: 24.00
    # Rectangle perimeter: 20.00
    # Circle radius: 5.0
    # Rectangle dimensions: 4.0 x 6.0
```

## Error

```
Assembly compilation failed:

error[CS0513]: 'BaseShapes.Shape.Area()' is abstract but it is contained in non-abstract type 'BaseShapes.Shape'
  --> base_shapes.cs:13:32
    |
 13 |     
    |     ^
    |

error[CS0513]: 'BaseShapes.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'BaseShapes.Shape'
  --> base_shapes.cs:14:32
    |
 14 |     print_shape_info(c, "Circle")
    |                                ^
    |


```

## Timing

- Generation: 100.24s
- Execution: 4.48s
