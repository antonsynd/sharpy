# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T08:47:07.144015
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both modules and tests inheritance

from math_utils import Calculator, Statistics, clamp, cube
from shapes import Rectangle, Circle, compute_shape_stats, Shape

def main():
    # Test Calculator static methods
    sum_val: float = Calculator.add(10.0, 5.0)
    print(sum_val)
    
    # Test cube function from math_utils
    cubed: float = cube(3.0)
    print(cubed)
    
    # Create shapes and test inheritance with override
    rect: Rectangle = Rectangle("R1", 4.0, 6.0)
    circle: Circle = Circle("C1", 2.5)
    
    # Test polymorphic method dispatch
    print(rect.area())
    print(circle.area())
    
    # Test compute_shape_stats which uses Shape interface
    stats = compute_shape_stats(rect)
    print(stats.area)
    
    # Test clamp function
    clamped: float = clamp(150.0, 0.0, 100.0)
    print(clamped)
    
    # Test Statistics class
    values: list[float] = [12.0, 45.0, 23.0, 8.0, 67.0]
    stats_calc: Statistics = Statistics(values)
    print(stats_calc.mean())

```

## Error

```
Assembly compilation failed:

error[CS1061]: '(double area, double perimeter)' does not contain a definition for 'Area' and no accessible extension method 'Area' accepting a first argument of type '(double area, double perimeter)' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpb1tq93zb/main.spy:25:45
    |
 25 |     print(stats.area)
    |                      ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpb1tq93zb/main.spy:4:60
    |
  4 | from shapes import Rectangle, Circle, compute_shape_stats, Shape
    |                                                            ^^^^^
    |


```

## Timing

- Generation: 156.57s
- Execution: 4.75s
