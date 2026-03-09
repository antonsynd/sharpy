# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T13:09:03.416304
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from geometry module
from geometry import Rectangle, Circle, calculate_total_area, format_measurement, Measurement

def main():
    # Create some shapes
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(2.0)

    # Test individual measurements
    print(format_measurement(rect))
    print(format_measurement(circle))

    # Create list and calculate total
    shapes: list[Measurable] = [rect, circle]
    total: float = calculate_total_area(shapes)
    print(total)

    # Test Measurement class
    m: Measurement = Measurement("width", 5.0)
    print(m.name)
    print(m.value)

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Geometry.Measurable.Measure()' is abstract but it is contained in non-abstract type 'Geometry.Measurable'
  --> geometry.cs:14:32
    |
 14 |     shapes: list[Measurable] = [rect, circle]
    |                                ^
    |

error[CS0513]: 'Geometry.Measurable.Describe()' is abstract but it is contained in non-abstract type 'Geometry.Measurable'
  --> geometry.cs:15:32
    |
 15 |     total: float = calculate_total_area(shapes)
    |                                ^
    |


```

## Timing

- Generation: 293.17s
- Execution: 4.85s
