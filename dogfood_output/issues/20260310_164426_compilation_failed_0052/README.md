# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T16:37:28.374505
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from shapes import Circle, Rectangle, Square
from utils import total_area, total_perimeter, describe_shape, create_sample_shapes

def main():
    shapes: list[Shape] = create_sample_shapes()
    print("=== Shape Details ===")
    for s in shapes:
        print(describe_shape(s))
    print("")
    print("=== Totals ===")
    area_total: float = total_area(shapes)
    perimeter_total: float = total_perimeter(shapes)
    print(f"Total area: {area_total:.2f}")
    print(f"Total perimeter: {perimeter_total:.2f}")
    c: Circle = Circle(3.0)
    r: Rectangle = Rectangle(2.0, 4.0)
    s: Square = Square(5.0)
    print("")
    print("=== Individual Shapes ===")
    print(f"Circle area: {c.area():.2f}")
    print(f"Rectangle perimeter: {r.perimeter():.2f}")
    print(f"Square side: {s.width}")

```

## Error

```
Assembly compilation failed:

error[CS0136]: A local or parameter named 's' cannot be declared in this scope because that name is used in an enclosing local scope to define a local or parameter
  --> /tmp/tmpie0y5pv1/main.spy:9:17
    |
  9 |     print("")
    |              ^
    |


```

## Timing

- Generation: 389.50s
- Execution: 4.98s
