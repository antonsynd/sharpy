# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T13:21:15.384438
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from shapes and math_utils modules

from shapes import Shape, Rectangle, Circle
from math_utils import square, factorial, sum_of_squares

def main():
    # Create shapes and calculate their properties
    rect: Rectangle = Rectangle(5.0, 3.0)
    circle: Circle = Circle(4.0)
    
    # Print shape areas and perimeters
    print(rect.area())
    print(rect.perimeter())
    print(circle.area())
    print(circle.perimeter())
    
    # Test math utilities
    print(square(7.0))
    print(factorial(5))
    
    # Test sum_of_squares with a list
    values: list[float] = [1.0, 2.0, 3.0, 4.0]
    print(sum_of_squares(values))

```

## Error

```
Assembly compilation failed:

error[CS0513]: 'Shapes.Shape.Area()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:14:32
    |
 14 |     print(circle.area())
    |                         ^
    |

error[CS0513]: 'Shapes.Shape.Perimeter()' is abstract but it is contained in non-abstract type 'Shapes.Shape'
  --> shapes.cs:15:32
    |
 15 |     print(circle.perimeter())
    |                              ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Shape' is never used
  --> /tmp/tmpejz0xj45/main.spy:3:20
    |
  3 | from shapes import Shape, Rectangle, Circle
    |                    ^^^^^
    |


```

## Timing

- Generation: 82.97s
- Execution: 4.67s
